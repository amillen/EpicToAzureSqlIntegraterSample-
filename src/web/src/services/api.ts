import type { WorkQueueListItem, WorkQueueDetail, WorkQueueOverview } from '../types';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

class ApiService {
  private async fetch(url: string, options?: RequestInit) {
    const response = await fetch(`${API_URL}${url}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options?.headers,
      },
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.status} ${response.statusText}`);
    }

    return response;
  }

  // Work Queue endpoints
  async getWorkQueue(params?: {
    queue?: string;
    status?: string;
    assignedTo?: string;
    search?: string;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  }): Promise<{ items: WorkQueueListItem[]; totalCount: number }> {
    const searchParams = new URLSearchParams();
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined && value !== null) {
          searchParams.append(key, value.toString());
        }
      });
    }

    const response = await this.fetch(`/api/workqueue?${searchParams}`);
    const items = await response.json();
    const totalCount = parseInt(response.headers.get('X-Total-Count') || '0');

    return { items, totalCount };
  }

  async getWorkQueueItem(id: number): Promise<WorkQueueDetail> {
    const response = await this.fetch(`/api/workqueue/${id}`);
    return response.json();
  }

  async assignToMe(id: number): Promise<void> {
    await this.fetch(`/api/workqueue/${id}/assign`, {
      method: 'POST',
    });
  }

  async updateStatus(id: number, status: string): Promise<void> {
    await this.fetch(`/api/workqueue/${id}/status`, {
      method: 'POST',
      body: JSON.stringify({ status }),
    });
  }

  async addNote(id: number, noteText: string): Promise<void> {
    await this.fetch(`/api/workqueue/${id}/notes`, {
      method: 'POST',
      body: JSON.stringify({ noteText }),
    });
  }

  // Claims endpoints
  async updateClaim(claimId: number, updates: { claimStatus?: string; payer?: string }): Promise<void> {
    await this.fetch(`/api/claims/${claimId}`, {
      method: 'PATCH',
      body: JSON.stringify(updates),
    });
  }

  // Analytics endpoints
  async getWorkQueueOverview(): Promise<WorkQueueOverview[]> {
    const response = await this.fetch('/api/analytics/workqueue-overview');
    return response.json();
  }
}

export const apiService = new ApiService();
