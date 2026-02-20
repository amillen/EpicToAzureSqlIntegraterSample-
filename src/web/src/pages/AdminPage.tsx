import { useState, useEffect } from 'react';
import {
  Box,
  Paper,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Grid,
  Card,
  CardContent,
} from '@mui/material';
import { apiService } from '../services/api';
import type { WorkQueueOverview } from '../types';

export function AdminPage() {
  const [overview, setOverview] = useState<WorkQueueOverview[]>([]);

  useEffect(() => {
    loadOverview();
  }, []);

  const loadOverview = async () => {
    try {
      const data = await apiService.getWorkQueueOverview();
      setOverview(data);
    } catch (error) {
      console.error('Failed to load overview:', error);
    }
  };

  const totalItems = overview.reduce((sum, item) => sum + item.itemCount, 0);
  const totalAtRisk = overview.reduce((sum, item) => sum + item.totalAtRisk, 0);

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h4" gutterBottom>
        Admin Dashboard
      </Typography>

      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Typography variant="body2" color="text.secondary">
                Total Items
              </Typography>
              <Typography variant="h4">{totalItems}</Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} sm={6} md={3}>
          <Card>
            <CardContent>
              <Typography variant="body2" color="text.secondary">
                Total At Risk
              </Typography>
              <Typography variant="h4">
                ${totalAtRisk.toLocaleString('en-US', { minimumFractionDigits: 0 })}
              </Typography>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <Paper sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Work Queue Overview
        </Typography>
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Queue</TableCell>
                <TableCell>Status</TableCell>
                <TableCell align="right">Count</TableCell>
                <TableCell align="right">At Risk</TableCell>
                <TableCell align="right">0-7 Days</TableCell>
                <TableCell align="right">8-30 Days</TableCell>
                <TableCell align="right">31-60 Days</TableCell>
                <TableCell align="right">&gt;60 Days</TableCell>
                <TableCell align="right">Avg Age</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {overview.map((item, index) => (
                <TableRow key={index}>
                  <TableCell>{item.queueName}</TableCell>
                  <TableCell>{item.status}</TableCell>
                  <TableCell align="right">{item.itemCount}</TableCell>
                  <TableCell align="right">
                    ${item.totalAtRisk.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                  </TableCell>
                  <TableCell align="right">{item.count_0_7Days}</TableCell>
                  <TableCell align="right">{item.count_8_30Days}</TableCell>
                  <TableCell align="right">{item.count_31_60Days}</TableCell>
                  <TableCell align="right">{item.count_Over60Days}</TableCell>
                  <TableCell align="right">{item.avgAgeDays}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      <Paper sx={{ p: 3, mt: 3 }}>
        <Typography variant="h6" gutterBottom>
          System Information
        </Typography>
        <Typography variant="body2">
          <strong>Environment:</strong> Development (Auth Bypass Mode)
        </Typography>
        <Typography variant="body2">
          <strong>Current User:</strong> dev-analyst@example.com
        </Typography>
        <Typography variant="body2">
          <strong>Role:</strong> BillingAnalyst
        </Typography>
      </Paper>
    </Box>
  );
}
