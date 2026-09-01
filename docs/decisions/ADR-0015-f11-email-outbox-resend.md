# ADR-0015 — E-mail transacional com Resend e PostgreSQL Outbox

- Status: Accepted
- Data: 2026-09-01

## Contexto

Notificações não podem acoplar regras de negócio a um provedor externo nem fazer uma operação principal falhar por indisponibilidade de e-mail. A entrega precisa tolerar retry, reprocessamento e múltiplas instâncias da API sem introduzir mensageria distribuída no Modular Monolith.

## Decisão

`IEmailSender` é o contrato neutro em Application. Infrastructure fornece `ResendEmailSender` e `FakeEmailSender`. Operações de negócio registram `EmailOutboxMessage` na mesma transação; um `BackgroundService` interno reivindica mensagens por atualização condicional atômica no PostgreSQL, envia e registra sucesso, retry limitado ou falha definitiva.

Cada mensagem possui `IdempotencyKey` estável e única localmente, também enviada ao Resend. Claims abandonados voltam à fila após lease configurável. O primeiro template é `WelcomeEmail`, informativo e sem senha, token ou função de confirmação de identidade.

Configuração e segredos são externos. Testes comuns usam exclusivamente o fake. Fila externa, password reset, e-mails de Billing e lembretes de agenda ficam adiados para seus próprios requisitos.

## Alternativas

- Envio síncrono na request: rejeitado por acoplar disponibilidade externa à operação principal.
- SDK/tipos do Resend em Application ou Domain: rejeitado por violar os boundaries.
- RabbitMQ, Kafka ou serviço de fila: adiado; complexidade operacional não é justificada no MVP.
- Retry somente no provedor: rejeitado; a proteção local e a rastreabilidade continuam obrigatórias.

## Consequências

- O cadastro e a mensagem de welcome são persistidos atomicamente.
- Mais de uma instância pode disputar trabalho sem selecionar deliberadamente a mesma mensagem.
- A garantia após falha entre envio externo e confirmação local depende também da chave de idempotência do provedor.
- Domínio e credencial Resend reais são requisitos operacionais não bloqueantes da implementação.
