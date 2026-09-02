import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from '../../core/config/app-config';
import { CommonModule } from '@angular/common';
import { NxBadge, NxButton, NxFormField, NxModal, NxPageHeader } from '../../shared/ui';

interface Appointment {
  id: string;
  customerId: string;
  professionalId: string;
  serviceId: string;
  startAtLocal: string;
  endAtLocal: string;
  status: string;
  notes?: string;
}
interface ScheduleContext {
  timeZoneId: string;
}

@Component({
  selector: 'app-schedule-page',
  imports: [FormsModule, CommonModule, NxPageHeader, NxButton, NxBadge, NxModal, NxFormField],
  templateUrl: './schedule-page.html',
  styleUrl: './schedule-page.scss',
})
export class SchedulePage {
  private http = inject(HttpClient);
  private config = inject(APP_CONFIG);
  appointments = signal<Appointment[]>([]);
  timeZoneId = signal('');
  mode = signal<'day' | 'week'>('day');
  editing = signal(false);
  showHours = signal(false);
  showBlock = signal(false);
  error = signal('');
  selectedDate = new Date().toISOString().slice(0, 10);
  form: any = {};
  hours: any = { dayOfWeek: 'Monday', startLocal: '09:00', endLocal: '18:00' };
  block: any = {};
  days = [
    ['Sunday', 'Domingo'],
    ['Monday', 'Segunda'],
    ['Tuesday', 'Terça'],
    ['Wednesday', 'Quarta'],
    ['Thursday', 'Quinta'],
    ['Friday', 'Sexta'],
    ['Saturday', 'Sábado'],
  ].map(([value, label]) => ({ value, label }));
  visible = computed(() => {
    const end = new Date(`${this.selectedDate}T00:00:00Z`);
    end.setUTCDate(end.getUTCDate() + (this.mode() === 'week' ? 7 : 1));
    const endDate = end.toISOString().slice(0, 10);
    return this.appointments().filter(
      (x) =>
        x.startAtLocal.slice(0, 10) >= this.selectedDate && x.startAtLocal.slice(0, 10) < endDate,
    );
  });
  constructor() {
    this.http
      .get<ScheduleContext>(`${this.config.apiBaseUrl}/scheduling/context`)
      .subscribe((x) => this.timeZoneId.set(x.timeZoneId));
    this.load();
  }
  toneFor(status: string) {
    const s = status.toLowerCase();
    return s === 'completed' || s === 'confirmed'
      ? 'green'
      : s === 'cancelled' || s === 'noshow'
        ? 'red'
        : s === 'inprogress'
          ? 'orange'
          : 'purple';
  }
  badgeTone(status: string): 'success' | 'danger' | 'warning' | 'info' {
    const s = status.toLowerCase();
    return s === 'completed' || s === 'confirmed'
      ? 'success'
      : s === 'cancelled' || s === 'noshow'
        ? 'danger'
        : s === 'inprogress'
          ? 'warning'
          : 'info';
  }
  statusLabel(status: string) {
    const m: Record<string, string> = {
      scheduled: 'Agendado',
      confirmed: 'Confirmado',
      inprogress: 'Em andamento',
      completed: 'Concluído',
      cancelled: 'Cancelado',
      noshow: 'Falta',
    };
    return m[status.toLowerCase()] ?? status;
  }
  setMode(value: 'day' | 'week') {
    this.mode.set(value);
    this.load();
  }
  load() {
    const start = new Date(`${this.selectedDate}T00:00:00Z`);
    start.setUTCDate(start.getUTCDate() - 1);
    const end = new Date(`${this.selectedDate}T00:00:00Z`);
    end.setUTCDate(end.getUTCDate() + (this.mode() === 'week' ? 8 : 2));
    this.http
      .get<Appointment[]>(`${this.config.apiBaseUrl}/appointments`, {
        params: { from: start.toISOString(), to: end.toISOString() },
      })
      .subscribe({ next: (x) => this.appointments.set(x), error: (e) => this.fail(e) });
  }
  saveAppointment() {
    const body = {
      customerId: this.form.customerId,
      professionalId: this.form.professionalId,
      serviceId: this.form.serviceId,
      startAt: this.form.startAt,
      notes: this.form.notes,
    };
    const request = this.form.id
      ? this.http.put(`${this.config.apiBaseUrl}/appointments/${this.form.id}`, body)
      : this.http.post(`${this.config.apiBaseUrl}/appointments`, body);
    request.subscribe({
      next: () => {
        this.closeForm();
        this.load();
      },
      error: (e) => this.fail(e),
    });
  }
  saveHours() {
    this.http
      .put(`${this.config.apiBaseUrl}/working-hours`, {
        ...this.hours,
        startLocal: `${this.hours.startLocal}:00`,
        endLocal: `${this.hours.endLocal}:00`,
      })
      .subscribe({ next: () => this.showHours.set(false), error: (e) => this.fail(e) });
  }
  saveBlock() {
    this.http.post(`${this.config.apiBaseUrl}/blocked-periods`, this.block).subscribe({
      next: () => {
        this.showBlock.set(false);
        this.block = {};
      },
      error: (e) => this.fail(e),
    });
  }
  edit(x: Appointment) {
    this.form = { ...x, startAt: x.startAtLocal };
    this.editing.set(true);
  }
  status(x: Appointment, status: string) {
    this.http
      .patch(`${this.config.apiBaseUrl}/appointments/${x.id}/status`, { status })
      .subscribe({ next: () => this.load(), error: (e) => this.fail(e) });
  }
  cancel(x: Appointment) {
    this.http
      .patch(`${this.config.apiBaseUrl}/appointments/${x.id}/cancel`, {})
      .subscribe({ next: () => this.load(), error: (e) => this.fail(e) });
  }
  closeForm() {
    this.editing.set(false);
    this.form = {};
  }
  private fail(e: any) {
    this.error.set(e?.error?.title || 'Não foi possível concluir a operação.');
    setTimeout(() => this.error.set(''), 4000);
  }
}
