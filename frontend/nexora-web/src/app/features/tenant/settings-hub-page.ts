import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

interface HubCard {
  title: string;
  description: string;
  icon: string;
  route: string;
  manage: boolean;
}

const CARDS: HubCard[] = [
  {
    title: 'Empresa',
    description: 'Nome, fuso horário e dados cadastrais.',
    icon: 'i-building',
    route: '/empresa',
    manage: true,
  },
  {
    title: 'Usuários e Equipe',
    description: 'Quem tem acesso e com qual papel.',
    icon: 'i-users',
    route: '/equipe',
    manage: false,
  },
  {
    title: 'Perfis e Papéis',
    description: 'Papéis de acesso da equipe.',
    icon: 'i-shield',
    route: '/perfis',
    manage: true,
  },
  {
    title: 'Permissões',
    description: 'O que cada papel pode fazer.',
    icon: 'i-lock',
    route: '/permissoes',
    manage: true,
  },
  {
    title: 'Plano e Assinatura',
    description: 'Plano atual, faturas e cobrança.',
    icon: 'i-card',
    route: '/assinatura',
    manage: false,
  },
  {
    title: 'Meu Perfil',
    description: 'Seus dados de acesso ao Nexora.',
    icon: 'i-user',
    route: '/perfil',
    manage: false,
  },
];

@Component({
  selector: 'app-settings-hub-page',
  imports: [RouterLink],
  templateUrl: './settings-hub-page.html',
  styleUrl: './settings-hub-page.scss',
})
export class SettingsHubPage {
  private readonly auth = inject(AuthService);
  readonly visibleCards = computed(() => {
    const canManage = this.auth.permissions().includes('tenant.manage');
    return CARDS.filter((c) => !c.manage || canManage);
  });
}
