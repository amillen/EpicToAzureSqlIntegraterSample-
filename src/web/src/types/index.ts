// API types
export interface WorkQueueListItem {
  workQueueItemId: number;
  claimId: number;
  epicClaimId: string;
  queueName: string;
  priority: number;
  status: string;
  assignedToUpn?: string;
  createdUtc: string;
  updatedUtc: string;
  ageDays: number;
  atRisk: number;
  payer?: string;
  claimStatus?: string;
  mrnHash?: string;
}

export interface ChargeLine {
  chargeLineId: number;
  cpt?: string;
  modifier?: string;
  units: number;
  chargeAmount: number;
  allowedAmount: number;
  paidAmount: number;
  denialCode?: string;
  serviceDate?: string;
}

export interface Claim {
  claimId: number;
  epicClaimId: string;
  payer?: string;
  billType?: string;
  totalCharge: number;
  totalAllowed: number;
  totalPaid: number;
  claimStatus?: string;
  lastEpicUpdateUtc?: string;
  epicEncounterId: string;
  admitDate?: string;
  dischargeDate?: string;
  epicPatientId: string;
  mrnHash?: string;
  gender?: string;
  dob?: string;
  chargeLines: ChargeLine[];
}

export interface TriageNote {
  triageNoteId: number;
  authorUpn: string;
  noteText: string;
  createdUtc: string;
}

export interface WorkQueueDetail {
  workQueueItemId: number;
  claimId: number;
  epicClaimId: string;
  queueName: string;
  priority: number;
  status: string;
  assignedToUpn?: string;
  createdUtc: string;
  updatedUtc: string;
  claim: Claim;
  notes: TriageNote[];
}

export interface WorkQueueOverview {
  queueName: string;
  status: string;
  itemCount: number;
  totalAtRisk: number;
  count_0_7Days: number;
  count_8_30Days: number;
  count_31_60Days: number;
  count_Over60Days: number;
  avgAgeDays: number;
}
