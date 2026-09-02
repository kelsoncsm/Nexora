import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TenantAdminService, TenantProfile } from './tenant-admin.service';
import { NxBadge, NxButton, NxCard, NxFormField, NxPageHeader } from '../../shared/ui';

const COMMON_ZONES = [
  'America/Sao_Paulo',
  'America/Bahia',
  'America/Fortaleza',
  'America/Recife',
  'America/Belem',
  'America/Manaus',
  'America/Cuiaba',
  'America/Campo_Grande',
  'America/Porto_Velho',
  'America/Rio_Branco',
  'America/Noronha',
  'America/Argentina/Buenos_Aires',
  'America/Montevideo',
  'America/Santiago',
  'America/Bogota',
  'America/Lima',
  'America/New_York',
  'Europe/Lisbon',
  'UTC',
];

@Component({
  selector: 'app-empresa-page',
  imports: [FormsModule, DatePipe, NxPageHeader, NxCard, NxFormField, NxButton, NxBadge],
  templateUrl: './empresa-page.html',
  styleUrl: './empresa-page.scss',
})
export class EmpresaPage {
  private readonly service = inject(TenantAdminService);
  readonly zones = COMMON_ZONES;
  readonly profile = signal<TenantProfile | null>(null);
  readonly name = signal('');
  readonly timeZoneId = signal('');
  readonly busy = signal(false);
  readonly error = signal('');
  readonly saved = signal(false);

  constructor() {
    this.load();
  }

  load() {
    this.service.profile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.name.set(p.name);
        this.timeZoneId.set(p.timeZoneId);
      },
      error: () => this.error.set('Não foi possível carregar os dados da empresa.'),
    });
  }

  save() {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.saved.set(false);
    this.service.updateProfile(this.name().trim(), this.timeZoneId().trim()).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.name.set(p.name);
        this.timeZoneId.set(p.timeZoneId);
        this.busy.set(false);
        this.saved.set(true);
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(
          e?.status === 403
            ? 'Você não tem permissão para editar a empresa.'
            : (e?.error?.title ?? e?.error ?? 'Não foi possível salvar as alterações.'),
        );
      },
    });
  }
}
