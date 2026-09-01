# AGENTS.md — Nexora

## 1. Propósito

Este arquivo define as regras permanentes para agentes de IA que trabalham no repositório **Nexora**.

Aplica-se principalmente a:
- Codex
- Claude Code
- outros agentes de implementação/revisão usados no projeto

O Nexora é uma plataforma **SaaS multi-nicho, multi-tenant e configurável**.

> Este arquivo contém regras operacionais. A arquitetura funcional e técnica está definida principalmente em `MASTER_PLAN.md` e nos documentos em `docs/`.

---

## 2. Hierarquia de autoridade

Ao trabalhar neste repositório, siga esta ordem:

1. Solicitação explícita atual do usuário.
2. Decisões arquiteturais aprovadas para o Nexora.
3. `MASTER_PLAN.md`.
4. ADRs aceitos em `docs/decisions/`.
5. Documentação técnica em `docs/`.
6. Prompt da fase atual em `prompts/`.
7. Este `AGENTS.md`.
8. Convenções já consolidadas no código.

Se houver conflito entre documentos, **não escolha silenciosamente uma interpretação**.

Pare a parte afetada, registre o conflito e solicite decisão arquitetural.

---

## 3. Responsabilidades

### Arquitetura

As decisões estruturais do Nexora devem ser tratadas como decisões arquiteturais controladas.

Não alterar unilateralmente:

- estratégia de multi-tenancy;
- Modular Monolith;
- boundaries dos módulos;
- estratégia de autenticação;
- estratégia de autorização;
- RBAC/permissões;
- modelo de Tenant;
- estratégia de planos/features;
- billing;
- contratos públicos importantes;
- estratégia de persistência;
- padrões de integração;
- arquitetura de pagamentos;
- estratégia de auditoria;
- estratégia de segurança.

Quando uma mudança arquitetural parecer necessária:

1. não implemente imediatamente;
2. descreva o problema;
3. apresente alternativas;
4. apresente impactos;
5. recomende uma opção;
6. aguarde aprovação;
7. registre ADR quando aprovado.

### Claude/Codex

São agentes de implementação e revisão.

Devem:

- implementar a fase solicitada;
- seguir a arquitetura existente;
- criar e executar testes;
- revisar código quando solicitado;
- reportar riscos;
- evitar decisões arquiteturais implícitas.

---

## 4. Idioma

Toda comunicação com o usuário deve ser em **português do Brasil (pt-BR)**.

Código deve seguir as convenções técnicas adotadas pelo projeto.

Nomes de classes, métodos, APIs e termos técnicos podem permanecer em inglês quando essa for a convenção definida no código.

---

## 5. Leitura obrigatória

Antes de implementar qualquer fase:

1. leia `MASTER_PLAN.md`;
2. leia este `AGENTS.md`;
3. leia `prompts/Fx.md` da fase solicitada;
4. leia ADRs relacionados;
5. leia documentação relacionada em `docs/`;
6. inspecione o código existente.

Nunca comece uma implementação relevante apenas pelo texto do prompt sem conhecer o estado real do repositório.

---

## 6. Regra de escopo

Trabalhe **somente na fase solicitada**.

Exemplo:

Se a fase atual for:

`F3 — Multi-Tenancy`

não implementar antecipadamente:

- F4 Platform Administration;
- F5 Plans & Features;
- F8 Scheduling;
- F9 Billing;
- F10 Payments.

Uma pequena preparação estrutural só é aceitável quando indispensável para a fase atual e não introduz comportamento futuro.

Em caso de dúvida, não antecipar.

---

## 7. Antes de editar

Execute uma inspeção inicial.

Verifique no mínimo:

```bash
git status
git branch --show-current
git log -5 --oneline
```

Depois:

- examine a estrutura;
- localize implementações relacionadas;
- identifique migrations existentes;
- identifique testes existentes;
- identifique alterações não commitadas;
- identifique arquivos fora do escopo já modificados.

Antes de alterações significativas, apresente um plano curto de implementação.

---

## 8. Proteção do trabalho existente

Alterações existentes no working tree devem ser consideradas trabalho importante.

Não:

```bash
git reset --hard
git clean -fd
git checkout -- .
git restore .
```

Não sobrescreva alterações que não foram produzidas pela tarefa atual.

Não reverta arquivos fora do escopo.

Se houver conflito com alteração pré-existente:

- preserve o arquivo;
- explique o conflito;
- solicite orientação se necessário.

---

## 9. Git

Por padrão, agentes **não devem fazer commit automaticamente**.

Também não devem:

- executar `git push`;
- criar tags;
- reescrever histórico;
- executar force push;
- apagar branches;
- fazer rebase destrutivo;
- usar comandos destrutivos sem autorização explícita.

Ao finalizar uma fase, apresente:

```text
git status
arquivos alterados
arquivos novos
arquivos removidos
```

O commit será feito somente quando solicitado.

---

## 10. Arquitetura base

A arquitetura inicial do Nexora é:

- Modular Monolith;
- Clean Architecture;
- DDD pragmático;
- SOLID;
- ASP.NET Core;
- Entity Framework Core;
- PostgreSQL;
- Angular;
- APIs REST;
- Docker para ambientes apropriados.

Não converter o sistema para microservices sem decisão arquitetural aprovada.

---

## 11. Modularidade

Os principais contextos/módulos previstos incluem:

```text
Identity
Tenancy
Administration
Plans
Features
Billing
Customers
Professionals
Services
Scheduling
Payments
Notifications
Reports
```

Evite dependências circulares.

Um módulo não deve acessar diretamente detalhes internos de outro módulo sem contrato apropriado.

Não criar abstrações apenas por estética.

Prefira a solução mais simples que preserve:

- domínio;
- segurança;
- testabilidade;
- isolamento;
- evolução futura.

---

## 12. Multi-tenancy — REGRA CRÍTICA

Multi-tenancy é requisito estrutural do Nexora.

Dados pertencentes a uma empresa devem possuir isolamento adequado.

### Regra principal

> Tenant A nunca pode ler, alterar ou excluir dados pertencentes ao Tenant B.

### TenantId

Nunca confiar no `TenantId` recebido do frontend como autoridade de segurança.

Evitar:

```csharp
var tenantId = request.TenantId;
```

como fonte confiável para determinar o tenant de uma operação autenticada.

Preferir contexto autenticado equivalente a:

```csharp
ITenantContext
```

O backend determina o tenant efetivo.

### Consultas

Toda consulta tenant-scoped deve aplicar isolamento.

### Escritas

Toda criação/alteração tenant-scoped deve validar o tenant no servidor.

### Relacionamentos

Ao associar entidades, valide que ambas pertencem ao tenant atual.

Exemplo:

```text
Appointment
Customer
Professional
Service
```

Todos precisam pertencer ao mesmo tenant.

---

## 13. Testes obrigatórios de tenancy

Sempre que uma nova entidade tenant-scoped for criada, adicionar testes de isolamento.

Cenário mínimo:

```text
Tenant A
Tenant B
```

Validar:

```text
A não lê B
A não altera B
A não exclui B
B não lê A
B não altera A
B não exclui A
```

Quando aplicável, testar tentativa de IDOR usando IDs válidos de outro tenant.

Não considerar um módulo tenant-scoped concluído sem evidência automatizada de isolamento.

---

## 14. Segurança

Segurança é responsabilidade do backend.

O Angular pode melhorar UX, mas não é autoridade de autorização.

Obrigatório considerar:

- autenticação;
- autorização;
- RBAC;
- permissions;
- tenant isolation;
- IDOR;
- validação;
- rate limiting quando aplicável;
- CORS;
- HTTPS em produção;
- secrets;
- logging seguro;
- auditoria;
- princípio do menor privilégio.

Nunca registrar:

- senha;
- hash de senha desnecessariamente;
- access token;
- refresh token bruto;
- API key;
- secret;
- dados de cartão.

---

## 15. Autenticação

Quando implementada:

- senha com algoritmo seguro;
- access token de duração limitada;
- refresh token revogável;
- validação de assinatura;
- validação de issuer/audience quando definidos;
- logout/revogação;
- proteção contra reutilização indevida conforme estratégia aprovada.

Não inventar mecanismo de autenticação paralelo.

---

## 16. Autorização

Preferir permissões explícitas.

Exemplos:

```text
customers.read
customers.create
customers.update
customers.delete

appointments.read
appointments.create
appointments.update
appointments.cancel
```

Não espalhar lógica como:

```csharp
if (user.Role == "Admin")
```

por controllers e services.

Centralize políticas de autorização conforme arquitetura aprovada.

---

## 17. Platform Admin x Tenant Admin

São escopos diferentes.

Não tratar Platform Admin como simplesmente um Tenant Admin mais poderoso.

Endpoints da plataforma devem ter proteção explícita.

Operações de Platform Admin devem ser auditáveis quando relevantes.

---

## 18. Planos e Features

Não hardcodar regras comerciais no frontend ou espalhadas pelo backend.

Evitar:

```csharp
if (plan.Name == "Premium")
```

Preferir resolução baseada em:

```text
Plan
Feature
PlanFeature
TenantFeatureOverride
Subscription
```

Acesso efetivo deve ser resolvido por serviço central apropriado.

---

## 19. Segmentos / Nichos

Não criar forks do produto por nicho.

Evitar:

```csharp
if (segment == "BARBERSHOP")
{
    // grande bloco de regras
}
```

Novo nicho deve preferencialmente ser habilitado por:

- configuração;
- features;
- templates;
- capabilities;
- módulos;
- regras específicas encapsuladas quando realmente necessárias.

Se adicionar um nicho exigir copiar grande parte do Core, reporte como problema arquitetural.

---

## 20. Billing

Billing do SaaS é diferente do financeiro operacional do tenant.

### Billing da plataforma

```text
Tenant
Subscription
Plan
Invoice/Charge
BillingPayment
```

### Pagamento operacional

```text
Customer
Appointment/Order
Payment
```

Não misturar os dois contextos.

---

## 21. Gateways de pagamento

Integrações devem utilizar abstração apropriada, por exemplo:

```csharp
IPaymentGateway
```

Não acoplar regras de negócio diretamente ao SDK de Mercado Pago, Stripe, Asaas, Pagar.me ou outro provedor.

Webhooks devem considerar:

- autenticidade;
- idempotência;
- duplicidade;
- reprocessamento;
- eventos fora de ordem quando aplicável;
- logging seguro.

Nunca considerar redirect do navegador como confirmação definitiva de pagamento.

---

## 22. Banco de dados

Banco principal:

```text
PostgreSQL
```

ORM:

```text
Entity Framework Core
```

Mudanças de schema devem ser versionadas por migrations.

Não alterar schema manualmente como solução permanente.

Não apagar ou reescrever migrations já utilizadas sem autorização.

---

## 23. Migrations

Antes de criar migration:

1. inspecione migrations existentes;
2. confirme o modelo atual;
3. valide o escopo da fase.

Depois:

- revise o código gerado;
- verifique operações destrutivas;
- verifique índices;
- teste aplicação da migration;
- quando possível, teste banco limpo.

Não criar migration vazia sem justificativa.

---

## 24. Integridade referencial

Relacionamentos tenant-scoped devem evitar cruzamento entre tenants.

Não basta verificar apenas FK existente.

Quando necessário, validar também que as entidades relacionadas pertencem ao tenant atual.

---

## 25. APIs

Padrões:

```text
/api/v1/...
```

Usar DTOs/contracts.

Não expor diretamente entidades do EF Core.

Padronizar:

- códigos HTTP;
- erros;
- validação;
- paginação;
- filtros;
- ordenação.

Não retornar stack trace ao cliente em produção.

---

## 26. Controllers

Controllers devem permanecer finos.

Evitar:

- regras de domínio extensas;
- queries complexas;
- acesso direto indiscriminado ao DbContext;
- decisões de autorização manuais repetidas.

Controllers coordenam HTTP; regras pertencem às camadas apropriadas.

---

## 27. Frontend Angular

O frontend deve:

- respeitar contratos da API;
- utilizar TypeScript estrito conforme configuração;
- usar componentes reutilizáveis;
- manter responsabilidades claras;
- usar guards/interceptors quando apropriado;
- tratar erros;
- ser responsivo.

O frontend pode esconder funcionalidades não permitidas, mas o backend deve negar a operação de qualquer forma.

---

## 28. Responsividade

Telas devem ser avaliadas pelo menos para:

- desktop;
- tablet;
- viewport móvel relevante.

Não considerar uma tela concluída apenas porque funciona em monitor grande.

---

## 29. Validação

Valide entrada no backend.

Validação do Angular é UX, não segurança.

Não confiar em:

- IDs;
- preços;
- permissões;
- TenantId;
- status;
- valores calculados;

somente porque vieram do frontend.

---

## 30. Datas e timezone

Não assumir timezone global.

Cada tenant poderá possuir timezone configurável.

Persistência e conversão devem seguir a estratégia definida pela arquitetura.

Agenda deve considerar explicitamente timezone.

---

## 31. Logs

Usar logging estruturado.

Quando possível incluir:

```text
CorrelationId
RequestId
UserId
TenantId
Operation
```

Sem registrar segredos ou dados sensíveis desnecessários.

---

## 32. Auditoria

Ações críticas devem ser auditáveis.

Exemplos:

- alteração de plano;
- alteração de permissões;
- desativação de tenant;
- ações administrativas;
- alterações financeiras relevantes;
- eventos importantes de autenticação.

Audit log não deve depender apenas de mensagens textuais frágeis.

---

## 33. Tratamento de erros

Usar mecanismo global consistente.

Diferenciar adequadamente:

```text
400 — entrada inválida
401 — não autenticado
403 — não autorizado
404 — recurso não encontrado
409 — conflito
422 — regra de negócio, se padrão adotado
429 — rate limit
500 — erro inesperado
```

Não usar `500` para regras esperadas de negócio.

---

## 34. Testes

Cada fase deve adicionar testes compatíveis com o risco da alteração.

Tipos:

- unitários;
- integração;
- E2E quando aplicável.

Não remover teste apenas porque passou a falhar após uma mudança.

Primeiro determine se:

- o código está errado;
- o teste está desatualizado;
- o requisito mudou.

---

## 35. Execução dos testes

Antes de finalizar uma fase, executar os testes relevantes.

Quando viável:

```bash
dotnet build
dotnet test
```

e no frontend os comandos definidos pelo projeto, como:

```bash
npm test
npm run build
```

Não afirmar que testes passaram se não foram executados.

Se o ambiente impedir execução:

```text
STATUS: BLOQUEADO/PARCIAL
```

e explique exatamente o motivo.

---

## 36. Qualidade

Não considere uma implementação concluída apenas porque compila.

Verifique:

- comportamento;
- segurança;
- tenancy;
- testes;
- migrations;
- regressões;
- contratos;
- tratamento de erros.

---

## 37. Dependências

Antes de adicionar pacote:

1. confirme necessidade;
2. verifique se já existe solução no projeto/framework;
3. avalie manutenção;
4. evite dependências para tarefas triviais.

Não atualizar versões de pacotes fora do escopo sem necessidade.

---

## 38. Refatorações

Evite refatorações grandes durante uma fase funcional sem necessidade.

Se encontrar dívida técnica relevante:

- documente;
- classifique impacto;
- proponha fase/tarefa específica.

Não misture uma feature pequena com reescrita estrutural extensa.

---

## 39. Performance

Não otimizar por especulação.

Primeiro priorizar:

- correção;
- segurança;
- clareza;
- índices coerentes;
- consultas eficientes.

Otimizações complexas devem ter evidência.

---

## 40. Microservices

Não criar microservices no MVP.

Extração futura somente quando houver necessidade demonstrável, como:

- escala independente;
- isolamento operacional;
- equipe independente;
- requisito de disponibilidade;
- boundary estável;
- custo/benefício favorável.

---

## 41. Mensageria

Não introduzir Kafka, RabbitMQ ou outra infraestrutura distribuída apenas por possibilidade futura.

Usar somente quando requisito real justificar.

---

## 42. IA no produto

Não adicionar funcionalidades de IA antecipadamente.

IA está fora do Core inicial e deverá entrar em fase específica.

---

## 43. Não inventar requisitos

Quando algo não estiver definido:

- não invente comportamento comercial;
- não invente preço;
- não invente limite;
- não invente política jurídica;
- não invente fluxo financeiro.

Identifique a decisão pendente.

---

## 44. Revisão por outro agente

Quando solicitado a revisar implementação feita por outro agente:

**não altere código inicialmente**.

Primeiro produza auditoria.

Classifique achados:

```text
CRÍTICO
ALTO
MÉDIO
BAIXO
INFORMATIVO
```

Para cada achado informe:

```text
Problema
Evidência
Impacto
Arquivo/local
Correção recomendada
```

Somente corrija após autorização quando o pedido for explicitamente uma auditoria.

---

## 45. Evitar falso positivo em revisão

Antes de reportar problema:

- leia a implementação completa relacionada;
- confira testes;
- confira configuração;
- confira middleware/interceptors/filters;
- confirme se a proteção já existe em outra camada.

Não reportar vulnerabilidade apenas por busca textual superficial.

---

## 46. Controle de contexto e tokens

Evite ler o repositório inteiro repetidamente.

Preferir:

1. documentação da fase;
2. módulos relacionados;
3. testes relacionados;
4. arquivos alterados;
5. dependências diretas.

Não despejar arquivos enormes no contexto sem necessidade.

Use buscas direcionadas.

---

## 47. Arquivos gerados

Não editar manualmente arquivos gerados quando existir processo oficial para regenerá-los.

Não versionar:

- secrets;
- binários desnecessários;
- `bin/`;
- `obj/`;
- `node_modules/`;
- arquivos temporários.

Respeitar `.gitignore`.

---

## 48. Documentação

Atualize documentação quando a implementação alterar:

- arquitetura;
- API pública;
- setup;
- configuração;
- migrations relevantes;
- segurança;
- decisões técnicas.

Mudança arquitetural aprovada deve gerar ou atualizar ADR.

---

## 49. Definition of Done da fase

Uma fase só pode ser reportada como `OK` quando:

```text
[ ] escopo implementado
[ ] build relevante passa
[ ] testes relevantes passam
[ ] migrations verificadas
[ ] tenancy verificada quando aplicável
[ ] autorização verificada
[ ] segurança revisada
[ ] documentação necessária atualizada
[ ] git diff revisado
[ ] nenhuma alteração fora do escopo foi incluída inadvertidamente
```

Se algum item obrigatório não puder ser validado:

```text
STATUS: PARCIAL
```

ou:

```text
STATUS: BLOQUEADO
```

---

## 50. Formato obrigatório do relatório final

Ao finalizar uma implementação:

```text
# Resultado

STATUS: OK | PARCIAL | BLOQUEADO

## Resumo
Breve descrição do realizado.

## Arquivos criados
- ...

## Arquivos alterados
- ...

## Arquivos removidos
- ...

## Banco / Migrations
- ...

## Segurança
- ...

## Multi-tenancy
- ...

## Testes executados
- comando
- resultado

## Build
- comando
- resultado

## Decisões tomadas
- ...

## Riscos / observações
- ...

## Pendências
- ...

## Git status
- ...

## Próximo passo recomendado
- ...
```

Não ocultar falhas.

---

## 51. Autonomia operacional dentro da fase

### Regra principal

Quando o usuário autorizar explicitamente uma fase, por exemplo:

`Execute a F1`

essa autorização vale para TODAS as operações normais necessárias para concluir SOMENTE a F1.

O agente deve trabalhar autonomamente até:

1. implementar o escopo;
2. restaurar/instalar dependências necessárias;
3. compilar;
4. executar testes;
5. corrigir erros pertencentes à fase;
6. repetir build/testes quando necessário;
7. revisar segurança e qualidade;
8. revisar o Git diff;
9. produzir o relatório final.

Não solicitar aprovação intermediária para operações rotineiras da fase quando o ambiente já permitir executá-las.

### Operações autorizadas dentro da fase

Quando necessárias ao escopo atual, o agente pode executar autonomamente:

- criar arquivos e diretórios;
- editar arquivos;
- gerar projetos;
- restaurar dependências;
- instalar dependências necessárias;
- executar `dotnet restore`;
- executar `dotnet build`;
- executar `dotnet test`;
- executar comandos EF Core necessários à fase;
- executar `npm install`;
- executar `npm ci`;
- executar `npm run build`;
- executar testes e lint do frontend;
- executar Angular CLI;
- executar Docker/Docker Compose para desenvolvimento e testes;
- iniciar backend/frontend localmente;
- executar health checks;
- executar scripts do próprio projeto;
- remover artefatos regeneráveis como `bin`, `obj`, `dist` e `node_modules` quando necessário;
- executar comandos Git somente de leitura/inspeção;
- corrigir automaticamente erros encontrados durante build, testes ou validação da fase.

A autorização operacional não permite violar nenhuma outra regra deste AGENTS.md.

### Ciclo autônomo permitido

O agente deve preferir:

FASE AUTORIZADA
↓
ANALISAR
↓
IMPLEMENTAR
↓
BUILD
↓
TESTAR
↓
ERRO?
↓
SIM → CORRIGIR → BUILD/TESTAR NOVAMENTE
↓
NÃO
↓
REVISAR
↓
RELATÓRIO
↓
PARAR

Não devolver o controle ao usuário apenas porque ocorreu um erro comum de compilação, dependência, lint ou teste que possa ser corrigido com segurança dentro do escopo atual.

### O que continua exigindo parada

Mesmo em modo autônomo, o agente deve parar quando encontrar:

- decisão arquitetural relevante ainda não aprovada;
- alteração de ADR Accepted;
- necessidade de implementar fase futura;
- credencial ou segredo que o usuário precisa fornecer;
- operação destrutiva sobre dados reais;
- conflito com alterações pré-existentes que possam ser perdidas;
- alteração significativa de arquitetura;
- alteração da estratégia de multi-tenancy;
- alteração da estratégia de autenticação/autorização;
- alteração estrutural de Billing/Payments;
- ação externa irreversível;
- bloqueio real que não possa ser resolvido dentro da fase.

Nesse caso retornar:

`DECISÃO ARQUITETURAL NECESSÁRIA`

ou:

`STATUS: BLOQUEADO`

conforme apropriado.

### Git

Autonomia operacional NÃO autoriza:

- `git commit`;
- `git push`;
- force push;
- rebase destrutivo;
- apagar branch;
- `git reset --hard`;
- `git clean -fd`;
- descartar alterações existentes.

Ao concluir a fase, deixar as alterações no working tree para revisão.

### Limite absoluto da autorização

Uma autorização para executar `Fx` nunca autoriza `Fx+1`.

Exemplo:

`Execute F2`

autoriza:

F2 → implementação → build → testes → correções → revisão → relatório.

NÃO autoriza:

F2 → F3.

Mesmo que:

- todos os testes estejam verdes;
- a F2 esteja 100% concluída;
- a F3 seja uma continuação óbvia;
- ainda exista contexto disponível;
- o agente considere mais eficiente continuar.


### Regra resumida

**AUTONOMIA TOTAL NAS OPERAÇÕES NORMAIS DA FASE ATUAL.**


**SEM COMMIT AUTOMÁTICO.**

**SEM DECISÃO ARQUITETURAL IMPLÍCITA.**
