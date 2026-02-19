import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Box,
  Paper,
  Typography,
  Grid,
  Chip,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Stack,
  Divider,
  Card,
  CardContent,
} from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { apiService } from '../services/api';
import { WorkQueueDetail } from '../types';

export function WorkQueueDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [item, setItem] = useState<WorkQueueDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [noteText, setNoteText] = useState('');

  useEffect(() => {
    if (id) {
      loadItem();
    }
  }, [id]);

  const loadItem = async () => {
    try {
      setLoading(true);
      const data = await apiService.getWorkQueueItem(parseInt(id!));
      setItem(data);
    } catch (error) {
      console.error('Failed to load work queue item:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleAssign = async () => {
    try {
      await apiService.assignToMe(parseInt(id!));
      await loadItem();
    } catch (error) {
      console.error('Failed to assign:', error);
    }
  };

  const handleStatusChange = async (newStatus: string) => {
    try {
      await apiService.updateStatus(parseInt(id!), newStatus);
      await loadItem();
    } catch (error) {
      console.error('Failed to update status:', error);
    }
  };

  const handleAddNote = async () => {
    if (!noteText.trim()) return;
    
    try {
      await apiService.addNote(parseInt(id!), noteText);
      setNoteText('');
      await loadItem();
    } catch (error) {
      console.error('Failed to add note:', error);
    }
  };

  if (loading || !item) {
    return (
      <Box sx={{ p: 3 }}>
        <Typography>Loading...</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 3 }}>
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={() => navigate('/workqueue')}
        sx={{ mb: 2 }}
      >
        Back to Work Queue
      </Button>

      <Typography variant="h4" gutterBottom>
        Claim Detail: {item.epicClaimId}
      </Typography>

      {/* Work Queue Info */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Work Queue Status
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={3}>
            <Typography variant="body2" color="text.secondary">
              Queue
            </Typography>
            <Chip label={item.queueName} color="primary" />
          </Grid>
          <Grid item xs={12} sm={3}>
            <Typography variant="body2" color="text.secondary">
              Status
            </Typography>
            <Chip label={item.status} color="warning" />
          </Grid>
          <Grid item xs={12} sm={3}>
            <Typography variant="body2" color="text.secondary">
              Priority
            </Typography>
            <Typography variant="body1">{item.priority}</Typography>
          </Grid>
          <Grid item xs={12} sm={3}>
            <Typography variant="body2" color="text.secondary">
              Assigned To
            </Typography>
            <Typography variant="body1">
              {item.assignedToUpn || 'Unassigned'}
            </Typography>
          </Grid>
        </Grid>

        <Stack direction="row" spacing={2} sx={{ mt: 2 }}>
          <Button variant="contained" onClick={handleAssign}>
            Assign to Me
          </Button>
          <Button variant="outlined" onClick={() => handleStatusChange('In Progress')}>
            Mark In Progress
          </Button>
          <Button variant="outlined" onClick={() => handleStatusChange('Resolved')}>
            Mark Resolved
          </Button>
        </Stack>
      </Paper>

      {/* Claim Info */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Claim Information
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={6} md={3}>
            <Typography variant="body2" color="text.secondary">
              Claim ID
            </Typography>
            <Typography variant="body1">{item.claim.epicClaimId}</Typography>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Typography variant="body2" color="text.secondary">
              Payer
            </Typography>
            <Typography variant="body1">{item.claim.payer || '-'}</Typography>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Typography variant="body2" color="text.secondary">
              Bill Type
            </Typography>
            <Typography variant="body1">{item.claim.billType || '-'}</Typography>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Typography variant="body2" color="text.secondary">
              Status
            </Typography>
            <Typography variant="body1">{item.claim.claimStatus || '-'}</Typography>
          </Grid>
          <Grid item xs={12} sm={4}>
            <Typography variant="body2" color="text.secondary">
              Total Charge
            </Typography>
            <Typography variant="body1">
              ${item.claim.totalCharge.toLocaleString('en-US', { minimumFractionDigits: 2 })}
            </Typography>
          </Grid>
          <Grid item xs={12} sm={4}>
            <Typography variant="body2" color="text.secondary">
              Total Allowed
            </Typography>
            <Typography variant="body1">
              ${item.claim.totalAllowed.toLocaleString('en-US', { minimumFractionDigits: 2 })}
            </Typography>
          </Grid>
          <Grid item xs={12} sm={4}>
            <Typography variant="body2" color="text.secondary">
              Total Paid
            </Typography>
            <Typography variant="body1">
              ${item.claim.totalPaid.toLocaleString('en-US', { minimumFractionDigits: 2 })}
            </Typography>
          </Grid>
        </Grid>

        <Divider sx={{ my: 2 }} />

        <Typography variant="subtitle1" gutterBottom>
          Patient Information
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={4}>
            <Typography variant="body2" color="text.secondary">
              MRN Hash
            </Typography>
            <Typography variant="body1">{item.claim.mrnHash || '-'}</Typography>
          </Grid>
          <Grid item xs={12} sm={4}>
            <Typography variant="body2" color="text.secondary">
              Gender
            </Typography>
            <Typography variant="body1">{item.claim.gender || '-'}</Typography>
          </Grid>
          <Grid item xs={12} sm={4}>
            <Typography variant="body2" color="text.secondary">
              DOB
            </Typography>
            <Typography variant="body1">
              {item.claim.dob ? new Date(item.claim.dob).toLocaleDateString() : '-'}
            </Typography>
          </Grid>
        </Grid>
      </Paper>

      {/* Charge Lines */}
      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Charge Lines
        </Typography>
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>CPT</TableCell>
                <TableCell>Modifier</TableCell>
                <TableCell>Service Date</TableCell>
                <TableCell align="right">Units</TableCell>
                <TableCell align="right">Charge</TableCell>
                <TableCell align="right">Allowed</TableCell>
                <TableCell align="right">Paid</TableCell>
                <TableCell>Denial Code</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {item.claim.chargeLines.map((line) => (
                <TableRow key={line.chargeLineId}>
                  <TableCell>{line.cpt || '-'}</TableCell>
                  <TableCell>{line.modifier || '-'}</TableCell>
                  <TableCell>
                    {line.serviceDate ? new Date(line.serviceDate).toLocaleDateString() : '-'}
                  </TableCell>
                  <TableCell align="right">{line.units}</TableCell>
                  <TableCell align="right">
                    ${line.chargeAmount.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                  </TableCell>
                  <TableCell align="right">
                    ${line.allowedAmount.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                  </TableCell>
                  <TableCell align="right">
                    ${line.paidAmount.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                  </TableCell>
                  <TableCell>
                    {line.denialCode ? (
                      <Chip label={line.denialCode} size="small" color="error" />
                    ) : (
                      '-'
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      {/* Notes */}
      <Paper sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Triage Notes
        </Typography>
        
        <Box sx={{ mb: 2 }}>
          <TextField
            fullWidth
            multiline
            rows={3}
            label="Add a note"
            value={noteText}
            onChange={(e) => setNoteText(e.target.value)}
          />
          <Button
            variant="contained"
            onClick={handleAddNote}
            sx={{ mt: 1 }}
            disabled={!noteText.trim()}
          >
            Add Note
          </Button>
        </Box>

        <Stack spacing={2}>
          {item.notes.map((note) => (
            <Card key={note.triageNoteId} variant="outlined">
              <CardContent>
                <Typography variant="body2" color="text.secondary">
                  {note.authorUpn} - {new Date(note.createdUtc).toLocaleString()}
                </Typography>
                <Typography variant="body1" sx={{ mt: 1 }}>
                  {note.noteText}
                </Typography>
              </CardContent>
            </Card>
          ))}
          {item.notes.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              No notes yet
            </Typography>
          )}
        </Stack>
      </Paper>
    </Box>
  );
}
