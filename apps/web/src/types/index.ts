export interface User {
  id: string;
  email: string;
  fullName: string;
  role: 'Admin' | 'Operator' | 'User';
  operatorId?: string;
  createdAt: string;
}

export interface Incident {
  id: string;
  title: string;
  description: string;
  type: IncidentType;
  severity: IncidentSeverity;
  status: IncidentStatus;
  source: IncidentSource;
  operatorId: string;
  location: Location;
  createdAt: string;
  resolvedAt?: string;
}

export enum IncidentType {
  Waste = 'Waste',
  Fighting = 'Fighting',
  UnauthorizedAccess = 'UnauthorizedAccess',
  Weapon = 'Weapon',
  AirQuality = 'AirQuality',
  Vandalism = 'Vandalism',
  Other = 'Other',
}

export enum IncidentSeverity {
  Low = 'Low',
  Medium = 'Medium',
  High = 'High',
  Critical = 'Critical',
}

export enum IncidentStatus {
  Open = 'Open',
  Assigned = 'Assigned',
  InProgress = 'InProgress',
  Resolved = 'Resolved',
  Closed = 'Closed',
}

export enum IncidentSource {
  Manual = 'Manual',
  AIDetection = 'AIDetection',
  Sensor = 'Sensor',
  CitizenReport = 'CitizenReport',
}

export interface Location {
  latitude: number;
  longitude: number;
  address?: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}
