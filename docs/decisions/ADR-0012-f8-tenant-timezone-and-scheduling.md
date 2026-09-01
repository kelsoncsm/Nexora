# ADR-0012 — Timezone do tenant e semântica temporal da agenda

- Status: Accepted
- Data: 2026-09-01

## Contexto

Agendamentos são ocorrências reais, enquanto horários de funcionamento e disponibilidade recorrente são regras civis locais. Tratar ambos como UTC, usar o timezone do navegador ou aceitar um timezone enviado pelo cliente como autoridade produz erros de isolamento temporal e em transições de horário de verão.

## Decisão

Cada `Tenant` possui `TimeZoneId` IANA obrigatório e validado no servidor. Registros existentes recebem `UTC` apenas durante a migration de compatibilidade; a coluna não mantém default e toda nova criação exige um valor explícito.

`ITenantTimeZoneProvider` obtém o timezone do tenant autenticado e `ITimeZoneService` centraliza conversões com `TimeZoneInfo`/`DateTimeOffset`. `Appointment` e `BlockedPeriod` persistem instantes UTC em PostgreSQL como `timestamp with time zone`. `WorkingHours` persiste dia da semana e `TimeOnly` locais, sem conversão permanente para UTC.

Contratos de escrita de ocorrências exigem ISO 8601 com offset explícito. O servidor confirma que o offset é válido para o horário civil e o timezone oficial do tenant. Horários inexistentes por DST são rejeitados; horários ambíguos exigem um dos offsets válidos, sem escolha silenciosa. Respostas incluem o instante UTC e a representação local calculada pelo servidor.

Relacionamentos de agenda usam chaves compostas com `TenantId`, e autorização usa as capabilities `appointments.read`, `appointments.create`, `appointments.update` e `appointments.cancel`.

## Alternativas

- Usar timezone do navegador: rejeitado porque não é configuração oficial da empresa.
- Persistir horários locais de appointments: rejeitado porque não identifica uma ocorrência inequívoca.
- Converter regras recorrentes definitivamente para UTC: rejeitado porque offsets podem mudar ao longo do ano.
- Adotar NodaTime: adiado; o stack atual cobre o escopo da F8 sem nova dependência.

## Consequências

- APIs e UI exibem horários no timezone oficial do tenant.
- Alterações de regras de timezone continuam centralizadas.
- Casos DST geram validação explícita.
- Uma futura adoção de NodaTime exige decisão arquitetural própria.
