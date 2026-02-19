using System.Data;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;

namespace EpicIngestor;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Epic Billing Data Ingestor");
        Console.WriteLine("==========================");

        // Get connection string from environment or use default
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? "Server=localhost,1433;Database=EpicBilling;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";

        // Get data directory from args or use default
        var dataDir = args.Length > 0 ? args[0] : "../../sample-data";
        
        Console.WriteLine($"Data directory: {Path.GetFullPath(dataDir)}");
        Console.WriteLine($"Database: {new SqlConnectionStringBuilder(connectionString).DataSource}");
        Console.WriteLine();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine("✓ Connected to database");

            // Find CSV files
            var claimFiles = Directory.GetFiles(dataDir, "epic_claims_*.csv");
            var chargeFiles = Directory.GetFiles(dataDir, "epic_charges_*.csv");

            Console.WriteLine($"Found {claimFiles.Length} claim file(s) and {chargeFiles.Length} charge file(s)");
            Console.WriteLine();

            // Process claim files
            foreach (var file in claimFiles)
            {
                Console.WriteLine($"Processing: {Path.GetFileName(file)}");
                await ProcessClaimFile(connection, file);
                Console.WriteLine("  ✓ Claims loaded to staging");
                
                await ExecuteStoredProcedure(connection, "dbo.sp_ProcessEpicClaims");
                Console.WriteLine("  ✓ Claims processed to core tables");
            }

            // Process charge files
            foreach (var file in chargeFiles)
            {
                Console.WriteLine($"Processing: {Path.GetFileName(file)}");
                await ProcessChargeFile(connection, file);
                Console.WriteLine("  ✓ Charges loaded to staging");
                
                await ExecuteStoredProcedure(connection, "dbo.sp_ProcessEpicCharges");
                Console.WriteLine("  ✓ Charges processed to core tables");
            }

            // Rebuild work queue
            Console.WriteLine("Rebuilding work queue...");
            await ExecuteStoredProcedure(connection, "dbo.sp_RebuildWorkQueue");
            Console.WriteLine("  ✓ Work queue rebuilt");

            Console.WriteLine();
            Console.WriteLine("✓ Ingestion complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    static async Task ProcessClaimFile(SqlConnection connection, string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        
        var records = csv.GetRecords<ClaimCsvRecord>().ToList();
        
        if (records.Count == 0)
        {
            Console.WriteLine("  (no records)");
            return;
        }

        var dataTable = new DataTable();
        dataTable.Columns.Add("EpicClaimId", typeof(string));
        dataTable.Columns.Add("EpicEncounterId", typeof(string));
        dataTable.Columns.Add("EpicPatientId", typeof(string));
        dataTable.Columns.Add("PatientDOB", typeof(DateTime));
        dataTable.Columns.Add("PatientGender", typeof(string));
        dataTable.Columns.Add("PatientMRNHash", typeof(string));
        dataTable.Columns.Add("AdmitDate", typeof(DateTime));
        dataTable.Columns.Add("DischargeDate", typeof(DateTime));
        dataTable.Columns.Add("Payer", typeof(string));
        dataTable.Columns.Add("BillType", typeof(string));
        dataTable.Columns.Add("TotalCharge", typeof(decimal));
        dataTable.Columns.Add("TotalAllowed", typeof(decimal));
        dataTable.Columns.Add("TotalPaid", typeof(decimal));
        dataTable.Columns.Add("ClaimStatus", typeof(string));
        dataTable.Columns.Add("LastEpicUpdateUtc", typeof(DateTime));

        foreach (var record in records)
        {
            var row = dataTable.NewRow();
            row["EpicClaimId"] = record.EpicClaimId;
            row["EpicEncounterId"] = record.EpicEncounterId;
            row["EpicPatientId"] = record.EpicPatientId;
            row["PatientDOB"] = record.PatientDOB ?? (object)DBNull.Value;
            row["PatientGender"] = record.PatientGender ?? (object)DBNull.Value;
            row["PatientMRNHash"] = record.PatientMRNHash ?? (object)DBNull.Value;
            row["AdmitDate"] = record.AdmitDate ?? (object)DBNull.Value;
            row["DischargeDate"] = record.DischargeDate ?? (object)DBNull.Value;
            row["Payer"] = record.Payer ?? (object)DBNull.Value;
            row["BillType"] = record.BillType ?? (object)DBNull.Value;
            row["TotalCharge"] = record.TotalCharge ?? (object)DBNull.Value;
            row["TotalAllowed"] = record.TotalAllowed ?? (object)DBNull.Value;
            row["TotalPaid"] = record.TotalPaid ?? (object)DBNull.Value;
            row["ClaimStatus"] = record.ClaimStatus ?? (object)DBNull.Value;
            row["LastEpicUpdateUtc"] = record.LastEpicUpdateUtc ?? (object)DBNull.Value;
            dataTable.Rows.Add(row);
        }

        using var bulkCopy = new SqlBulkCopy(connection);
        bulkCopy.DestinationTableName = "staging.EpicClaimCsv";
        await bulkCopy.WriteToServerAsync(dataTable);
        
        Console.WriteLine($"  {records.Count} claim record(s)");
    }

    static async Task ProcessChargeFile(SqlConnection connection, string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        
        var records = csv.GetRecords<ChargeCsvRecord>().ToList();
        
        if (records.Count == 0)
        {
            Console.WriteLine("  (no records)");
            return;
        }

        var dataTable = new DataTable();
        dataTable.Columns.Add("EpicClaimId", typeof(string));
        dataTable.Columns.Add("CPT", typeof(string));
        dataTable.Columns.Add("Modifier", typeof(string));
        dataTable.Columns.Add("Units", typeof(int));
        dataTable.Columns.Add("ChargeAmount", typeof(decimal));
        dataTable.Columns.Add("AllowedAmount", typeof(decimal));
        dataTable.Columns.Add("PaidAmount", typeof(decimal));
        dataTable.Columns.Add("DenialCode", typeof(string));
        dataTable.Columns.Add("ServiceDate", typeof(DateTime));

        foreach (var record in records)
        {
            var row = dataTable.NewRow();
            row["EpicClaimId"] = record.EpicClaimId;
            row["CPT"] = record.CPT ?? (object)DBNull.Value;
            row["Modifier"] = record.Modifier ?? (object)DBNull.Value;
            row["Units"] = record.Units ?? (object)DBNull.Value;
            row["ChargeAmount"] = record.ChargeAmount ?? (object)DBNull.Value;
            row["AllowedAmount"] = record.AllowedAmount ?? (object)DBNull.Value;
            row["PaidAmount"] = record.PaidAmount ?? (object)DBNull.Value;
            row["DenialCode"] = record.DenialCode ?? (object)DBNull.Value;
            row["ServiceDate"] = record.ServiceDate ?? (object)DBNull.Value;
            dataTable.Rows.Add(row);
        }

        using var bulkCopy = new SqlBulkCopy(connection);
        bulkCopy.DestinationTableName = "staging.EpicChargeCsv";
        await bulkCopy.WriteToServerAsync(dataTable);
        
        Console.WriteLine($"  {records.Count} charge line record(s)");
    }

    static async Task ExecuteStoredProcedure(SqlConnection connection, string procedureName)
    {
        await using var command = new SqlCommand(procedureName, connection);
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 300; // 5 minutes
        await command.ExecuteNonQueryAsync();
    }
}

// CSV record classes
public class ClaimCsvRecord
{
    public string EpicClaimId { get; set; } = null!;
    public string EpicEncounterId { get; set; } = null!;
    public string EpicPatientId { get; set; } = null!;
    public DateTime? PatientDOB { get; set; }
    public string? PatientGender { get; set; }
    public string? PatientMRNHash { get; set; }
    public DateTime? AdmitDate { get; set; }
    public DateTime? DischargeDate { get; set; }
    public string? Payer { get; set; }
    public string? BillType { get; set; }
    public decimal? TotalCharge { get; set; }
    public decimal? TotalAllowed { get; set; }
    public decimal? TotalPaid { get; set; }
    public string? ClaimStatus { get; set; }
    public DateTime? LastEpicUpdateUtc { get; set; }
}

public class ChargeCsvRecord
{
    public string EpicClaimId { get; set; } = null!;
    public string? CPT { get; set; }
    public string? Modifier { get; set; }
    public int? Units { get; set; }
    public decimal? ChargeAmount { get; set; }
    public decimal? AllowedAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public string? DenialCode { get; set; }
    public DateTime? ServiceDate { get; set; }
}
