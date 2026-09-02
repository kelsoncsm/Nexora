import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { NxPageHeader } from '../../shared/ui';

interface HubCard {
  title: string;
  description: string;
  icon: string;
  route: string;
  group: string;
  manage: boolean;
}

const CARDS: HubCard[] = [
  {
    title: 'Empresa',
    description: 'Nome, fuso horário e dados cadastrais.',
    icon: 'i-building',
    route: '/empresa',
    group: 'Empresa',
    manage: true,
  },
  {
    title: 'Plano e Assinatura',
    description: 'Plano atual, faturas e cobrança.',
    icon: 'i-card',
    route: '/assinatura',
    group: 'Empresa',
    manage: false,
  },
  {
    title: 'Usuários e Equipe',
    description: 'Quem tem acesso e com qual papel.',
    icon: 'i-users',
    route: '/equipe',
    group: 'Acesso',
    manage: false,
  },
  {
    title: 'Perfis e Papéis',
    description: 'Papéis de acesso da equipe.',
    icon: 'i-shield',
    route: '/perfis',
    group: 'Acesso',
    manage: true,
  },
  {
    title: 'Permissões',
    description: 'O que cada papel pode fazer.',
    icon: 'i-lock',
    route: '/permissoes',
    group: 'Acesso',
    manage: true,
  },
  {
    title: 'Meu Perfil',
    description: 'Seus dados de acesso ao Nexora.',
    icon: 'i-user',
    route: '/perfil',
    group: 'Conta',
    manage: false,
  },
];

@Component({
  selector: 'app-settings-hub-page',
  imports: [RouterLink, NxPageHeader],
  templateUrl: './settings-hub-page.html',
  styleUrl: './settings-hub-page.scss',
})
export class SettingsHubPage {
  private readonly auth = inject(AuthService);
  readonly groups = computed(() => {
    const canManage = this.auth.permissions().includes('tenant.manage');
    const visible = CARDS.filter((c) => !c.manage || canManage);
    const names = [...new Set(visible.map((c) => c.group))];
    return names.map((name) => ({ name, cards: visible.filter((c) => c.group === name) }));
  });
}
