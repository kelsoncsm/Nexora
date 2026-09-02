export interface NxNavItem {
  label: string;
  route: string;
  icon: string;
  exact?: boolean;
}

export interface NxNavGroup {
  label?: string;
  items: NxNavItem[];
}

export interface NxShellUser {
  label: string;
  role: string;
  initials: string;
}

export interface NxShellPlan {
  code: string;
  statusLabel: string;
  active: boolean;
  tenantLabel: string;
}
