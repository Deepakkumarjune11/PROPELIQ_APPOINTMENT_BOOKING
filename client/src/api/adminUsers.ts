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
  department?: string;
}

export interface UpdateUserRequest {
  name: string;
  email: string;
  role: string;
  department?: string;
}

export interface AssignRoleRequest {
  role: string;
  permissionsBitfield: number;
}
