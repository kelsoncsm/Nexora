# ADR-0011 — RBAC tenant-scoped

- Status: Accepted
- Data: 2026-09-01

## Decisão

Capabilities são chaves globais estáveis. `TenantRole` pertence a um tenant, `TenantRolePermission` associa capabilities ao papel e `TenantUser` referencia exclusivamente `TenantRoleId`. A antiga string `TenantUser.Role` é migrada e removida.

Sessões tenant-scoped carregam `tenant_id`; a cada requisição o servidor revalida vínculo, tenant, papel e permissões, substituindo claims de permission antes das policies. Assim, alterações têm efeito imediato e permissões de outro tenant ou de Platform Admin não vazam ao escopo operacional.

## Consequências

- Um usuário pode ter capabilities diferentes em cada tenant.
- Endpoints usam policies `customers.*`, sem condicionais por nome de papel.
- Platform Admin continua global e não recebe acesso operacional implícito.
- A atribuição de papel valida que vínculo e papel pertencem ao mesmo tenant.
