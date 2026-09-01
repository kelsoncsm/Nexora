import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Location } from '@angular/common';

type ErrorCode = 403 | 404 | 500;

const COPY: Record<ErrorCode, { title: string; message: string }> = {
  403: { title: 'Acesso negado', message: 'Você não tem permissão para acessar esta área.' },
  404: {
    title: 'Página não encontrada',
    message: 'O endereço que você tentou abrir não existe ou foi movido.',
  },
  500: {
    title: 'Erro inesperado',
    message: 'Algo deu errado ao processar sua solicitação. Tente novamente em instantes.',
  },
};

@Component({
  selector: 'app-error-page',
  imports: [RouterLink],
  templateUrl: './error-page.html',
  styleUrl: './error-page.scss',
})
export class ErrorPage {
  private route = inject(ActivatedRoute);
  private location = inject(Location);
  readonly code = computed<ErrorCode>(() => {
    const c = Number(this.route.snapshot.data['code']);
    return c === 403 || c === 404 || c === 500 ? c : 404;
  });
  readonly copy = computed(() => COPY[this.code()]);
  back() {
    this.location.back();
  }
}
