import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TablePagination,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Chip,
  Typography,
  Button,
  Stack,
} from '@mui/material';
import { apiService } from '../services/api';
import { WorkQueueListItem } from '../types';

export function WorkQueueList() {
  const navigate = useNavigate();
  const [items, setItems] = useState<WorkQueueListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  
  // Filters
  const [queue, setQueue] = useState('');
  const [status, setStatus] = useState('');
  const [assignedTo, setAssignedTo] = useState('');
  const [search, setSearch] = useState('');
  
  // Pagination
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);

  useEffect(() => {
    loadWorkQueue();
  }, [queue, status, assignedTo, search, page, rowsPerPage]);

  const loadWorkQueue = async () => {
    try {
      setLoading(true);
      const result = await apiService.getWorkQueue({
        queue: queue || undefined,
        status: status || undefined,
        assignedTo: assignedTo || undefined,
        search: search || undefined,
        page: page + 1,
        pageSize: rowsPerPage,
      });
      setItems(result.items);
      setTotalCount(result.totalCount);
    } catch (error) {
      console.error('Failed to load work queue:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleRowClick = (id: number) => {
    navigate(`/workqueue/${id}`);
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'New': return 'primary';
      case 'In Progress': return 'warning';
      case 'Resolved': return 'success';
      default: return 'default';
    }
  };

  const getQueueColor = (queue: string) => {
    switch (queue) {
      case 'Denials': return 'error';
      case 'Underpaid': return 'warning';
      case 'Clean': return 'success';
      default: return 'default';
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h4" gutterBottom>
        Work Queue
      </Typography>

      {/* Filters */}
      <Paper sx={{ p: 2, mb: 2 }}>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          <FormControl sx={{ minWidth: 150 }}>
            <InputLabel>Queue</InputLabel>
            <Select
              value={queue}
              label="Queue"
              onChange={(e) => setQueue(e.target.value)}
            >
              <MenuItem value="">All</MenuItem>
              <MenuItem value="Denials">Denials</MenuItem>
              <MenuItem value="Underpaid">Underpaid</MenuItem>
              <MenuItem value="Clean">Clean</MenuItem>
            </Select>
          </FormControl>

          <FormControl sx={{ minWidth: 150 }}>
            <InputLabel>Status</InputLabel>
            <Select
              value={status}
              label="Status"
              onChange={(e) => setStatus(e.target.value)}
            >
              <MenuItem value="">All</MenuItem>
              <MenuItem value="New">New</MenuItem>
              <MenuItem value="In Progress">In Progress</MenuItem>
              <MenuItem value="Resolved">Resolved</MenuItem>
            </Select>
          </FormControl>

          <FormControl sx={{ minWidth: 150 }}>
            <InputLabel>Assigned To</InputLabel>
            <Select
              value={assignedTo}
              label="Assigned To"
              onChange={(e) => setAssignedTo(e.target.value)}
            >
              <MenuItem value="">All</MenuItem>
              <MenuItem value="me">Assigned to Me</MenuItem>
              <MenuItem value="unassigned">Unassigned</MenuItem>
            </Select>
          </FormControl>

          <TextField
            label="Search (Claim ID / MRN)"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            sx={{ minWidth: 250 }}
          />

          <Button onClick={loadWorkQueue} variant="outlined">
            Refresh
          </Button>
        </Stack>
      </Paper>

      {/* Table */}
      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Claim ID</TableCell>
              <TableCell>Queue</TableCell>
              <TableCell>Priority</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Payer</TableCell>
              <TableCell align="right">At Risk</TableCell>
              <TableCell align="right">Age (days)</TableCell>
              <TableCell>Assigned To</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={8} align="center">
                  Loading...
                </TableCell>
              </TableRow>
            ) : items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} align="center">
                  No items found
                </TableCell>
              </TableRow>
            ) : (
              items.map((item) => (
                <TableRow
                  key={item.workQueueItemId}
                  hover
                  onClick={() => handleRowClick(item.workQueueItemId)}
                  sx={{ cursor: 'pointer' }}
                >
                  <TableCell>{item.epicClaimId}</TableCell>
                  <TableCell>
                    <Chip
                      label={item.queueName}
                      color={getQueueColor(item.queueName)}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>{item.priority}</TableCell>
                  <TableCell>
                    <Chip
                      label={item.status}
                      color={getStatusColor(item.status)}
                      size="small"
                    />
                  </TableCell>
                  <TableCell>{item.payer || '-'}</TableCell>
                  <TableCell align="right">
                    ${item.atRisk.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                  </TableCell>
                  <TableCell align="right">{item.ageDays}</TableCell>
                  <TableCell>{item.assignedToUpn || 'Unassigned'}</TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
        <TablePagination
          component="div"
          count={totalCount}
          page={page}
          onPageChange={(_, newPage) => setPage(newPage)}
          rowsPerPage={rowsPerPage}
          onRowsPerPageChange={(e) => {
            setRowsPerPage(parseInt(e.target.value, 10));
            setPage(0);
          }}
        />
      </TableContainer>
    </Box>
  );
}
