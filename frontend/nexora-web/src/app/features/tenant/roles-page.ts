import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TenantAdminService, TenantRole } from './tenant-admin.service';

@Component({
  selector: 'app-roles-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './roles-page.html',
  styleUrl: './roles-page.scss',
})
export class RolesPage {
  private readonly service = inject(TenantAdminService);
  readonly roles = signal<TenantRole[]>([]);
  readonly editing = signal<{ id: string | null; name: string; description: string } | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly saved = signal(false);

  constructor() {
    this.load();
  }

  load() {
    this.service.roles().subscribe({
      next: (r) => this.roles.set(r),
      error: (e) =>
        this.error.set(
          e?.status === 403
            ? 'Você não tem permissão para gerenciar papéis.'
            : 'Não foi possível carregar os papéis.',
        ),
    });
  }

  openCreate() {
    this.saved.set(false);
    this.editing.set({ id: null, name: '', description: '' });
  }
  openEdit(r: TenantRole) {
    this.saved.set(false);
    this.editing.set({ id: r.id, name: r.name, description: r.description });
  }

  submit() {
    const e = this.editing();
    if (!e || this.busy() || !e.name.trim()) return;
    this.busy.set(true);
    this.error.set('');
    const done = () => {
      this.busy.set(false);
      this.saved.set(true);
      this.editing.set(null);
      this.load();
    };
    const fail = (err: { status?: number; error?: { title?: string } }) => {
      this.busy.set(false);
      this.error.set(
        err?.status === 409
          ? 'Já existe um papel com esse nome.'
          : err?.status === 403
            ? 'Você não tem permissão para gerenciar papéis.'
            : (err?.error?.title ?? 'Não foi possível salvar o papel.'),
      );
    };
    const req = e.id
      ? this.service.updateRole(e.id, e.name.trim(), e.description.trim())
      : this.service.createRole(e.name.trim(), e.description.trim(), []);
    req.subscribe({ next: done, error: fail });
  }
}
