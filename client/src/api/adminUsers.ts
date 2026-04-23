export interface AdminUserDto {
  id: string;
  name: string;
  email: string;
  role: 'Patient' | 'Staff' | 'Admin';
  department?: string;
  isActive: boolean;
  permissionsBitfield: number;
  createdAt: string;
}

export interface CreateUserRequest {
  name: string;
  email: string;
  role: string;
  /** Required when role is 'Staff'. Must be FrontDesk | CallCenter | ClinicalReviewer */
  staffRole?: string;
  password: string;
  department?: string;
}

export interface UpdateUserRequest {
  name: string;
  email: string;
  role: string;
  /** Optional — if omitted the existing password is preserved */
  password?: string;
  department?: string;
}

export interface AssignRoleRequest {
  /** Maps to backend StaffRole enum: FrontDesk | CallCenter | ClinicalReviewer */
  staffRole: string;
  permissionsBitfield: number;
}
