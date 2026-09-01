# Verticais

## Barbearia / Salão

A F15 introduz o primeiro vertical como configuração sobre o Core existente.

1. O usuário cria a empresa e escolhe `BARBERSHOP_SALON`.
2. A migration garante a disponibilidade desse segmento.
3. A UI abre `/configuracao-inicial` após emitir a sessão tenant.
4. `GET /api/v1/vertical-setup` resolve o template pelo segmento persistido.
5. O usuário seleciona zero ou mais presets e informa duração/preço.
6. `POST /api/v1/vertical-setup/apply` cria somente os serviços selecionados.

Corte, Barba, Corte + Barba, Sobrancelha, Manicure, Pedicure e Alongamento são sugestões. Serviços personalizados continuam disponíveis pelo catálogo normal.

## Outro nicho

Adicione uma entrada em `VerticalTemplates:Templates`, usando como chave o código de um `BusinessSegment`. O contrato aceita textos, terminologia e presets categorizados; não é necessário copiar módulos ou adicionar condicionais aos endpoints.

## Segurança e tenancy

Os endpoints exigem as permissões existentes de serviços. O backend obtém o `TenantId` do token, resolve o segmento no banco e limita consultas e escritas ao mesmo tenant.
