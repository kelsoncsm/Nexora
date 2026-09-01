\# PROMPT — Adaptar frontend do Nexora ao novo Design System visual

Você está trabalhando no projeto \*\*Nexora\*\*, um SaaS multinicho com arquitetura já definida e validada.

A arquitetura inicial do sistema já foi concluída. Sua tarefa agora é \*\*adaptar o frontend existente para o novo padrão visual do Nexora\*\*, usando como referência as imagens fornecidas nesta sessão.

\## Objetivo

Transformar o frontend atual em uma interface SaaS:

* moderna;
* premium;
* limpa;
* responsiva;
* visualmente consistente;
* fácil de usar;
* preparada para múltiplos nichos;
* preparada para múltiplos tenants;
* preparada para módulos diferentes por plano.

O resultado deve seguir o estilo visual das imagens de referência:

* sidebar clara;
* fundo principal muito claro;
* cards brancos;
* sombras discretas;
* bordas suaves;
* bastante espaço em branco;
* cor primária em roxo/azul;
* elementos secundários em verde, azul, laranja e vermelho;
* visual corporativo moderno;
* sem aparência de template administrativo antigo;
* sem exagero de cores ou efeitos.

\---

\# REGRA PRINCIPAL

\## NÃO ALTERAR A ARQUITETURA EXISTENTE

Antes de modificar qualquer arquivo:

1. Analise a estrutura atual do frontend.
1. Identifique:

* shell da aplicação;
* layout;
* rotas;
* guards;
* interceptors;
* autenticação;
* serviços;
* estado;
* multi-tenancy;
* controle de plano;
* permissões;
* módulos;
* componentes compartilhados.
1. Preserve completamente as decisões arquiteturais existentes.

Não modificar sem necessidade:

* backend;
* contratos da API;
* autenticação;
* autorização;
* JWT;
* multi-tenancy;
* regras de assinatura;
* Plan;
* Subscription;
* billing;
* migrations;
* banco de dados;
* regras de negócio;
* endpoints existentes.

Se encontrar alguma necessidade de mudança arquitetural relevante, PARE e apenas documente o problema.

Não implemente mudança arquitetural por conta própria.

\---

\# ESCOPO DESTA FASE

Esta fase é predominantemente de:

* UI;
* UX;
* layout;
* responsividade;
* Design System;
* organização visual;
* adaptação dos componentes existentes.

Não recrie funcionalidades que já existem.

Não substitua regras de negócio existentes por mocks.

Não quebre integrações já existentes.

\---

\# 1. CRIAR O APPLICATION SHELL DO NEXORA

Criar/refatorar o shell principal para ter:

\## Sidebar lateral clara

Características:

* fundo branco ou cinza muito claro;
* borda direita discreta;
* largura confortável;
* possibilidade de recolher;
* logo Nexora no topo;
* ícones simples;
* textos bem espaçados;
* item ativo com fundo roxo muito claro;
* ícone e texto ativo em roxo;
* hover discreto.

Estrutura esperada:

* Visão Geral
* Dashboard
* Clientes
* Agenda
* Atendimentos
* Financeiro
* Produtos & Serviços
* Relatórios
* Comunicação
* demais módulos existentes

Separar visualmente:

\### Principal

Itens funcionais do sistema.

\### Configurações

* Empresa
* Usuários
* Planos & Assinatura
* Integrações
* Configurações

Não inventar módulos que não existam no projeto.

Se as imagens de referência mostrarem itens que ainda não existem, usar apenas como inspiração visual.

\---

\# 2. CONTEXTO DO TENANT

Deve existir no shell uma identificação clara da empresa atual.

Exemplo visual:

Clínica Sorriso

Matriz

ou:

Empresa XPTO

Unidade Principal

Usar dados reais existentes no contexto do tenant.

Nunca hardcodar nome de empresa.

Se o frontend ainda não expõe o tenant atual de forma adequada, reutilizar os serviços existentes.

Não criar nova regra de multi-tenancy.

\---

\# 3. PLANO DO TENANT

Na parte inferior da sidebar, quando fizer sentido, exibir um pequeno card com:

* nome do plano;
* status;
* informações úteis já disponíveis;
* botão para gerenciar plano, caso essa rota já exista.

Exemplo:

Plano Professional

8 de 15 módulos ativos

Gerenciar plano

Usar dados reais.

Não simular assinatura.

Não duplicar a lógica de Subscription no frontend.

\---

\# 4. TOPBAR

Criar uma topbar clara com:

* botão para recolher/abrir sidebar;
* campo de busca visual;
* área de notificações;
* ajuda;
* alternância light/dark, se já suportada ou se for simples adicionar sem quebrar arquitetura;
* avatar;
* nome do usuário;
* papel/perfil do usuário;
* menu de conta.

Os dados do usuário devem vir da autenticação existente.

Não hardcodar usuário.

\---

\# 5. DESIGN SYSTEM DO NEXORA

Centralizar tokens visuais.

Criar uma camada clara para:

* cores;
* tipografia;
* espaçamentos;
* bordas;
* radius;
* sombras;
* estados;
* breakpoints.

Sugestão conceitual:

Primary:

* roxo/azul Nexora

Background:

* #F7F8FC ou equivalente

Surface:

* branco

Text:

* azul-marinho/cinza escuro

Success:

* verde

Warning:

* laranja

Danger:

* vermelho

Info:

* azul

Não espalhar cores hardcoded em dezenas de componentes.

Centralizar o máximo possível.

\---

\# 6. COMPONENTES VISUAIS REUTILIZÁVEIS

Sempre que fizer sentido, criar ou consolidar componentes reutilizáveis para:

* Card
* KPI Card
* Page Header
* Section Header
* Button
* Badge
* Empty State
* Loading/Skeleton
* Table Wrapper
* Filter Bar
* Search Input
* Modal
* Drawer
* Toast
* Status Badge
* Stat Card
* Quick Action Card

Evitar duplicação visual.

Não criar abstrações exageradas.

\---

\# 7. DASHBOARD

Adaptar o Dashboard para seguir o estilo das imagens.

O dashboard deve ter, usando apenas dados e funcionalidades já existentes:

\## Cabeçalho

Exemplo:

Bom dia, {{usuario.nome}}

Aqui está o resumo da sua empresa hoje.

\## Cards de indicadores

Possíveis cards:

* receita;
* clientes;
* atendimentos;
* pendências;
* outros KPIs já existentes.

Cada card pode conter:

* título;
* valor;
* variação;
* ícone;
* pequena representação visual.

Não inventar dados.

Se algum dado ainda não existir, não criar número fictício.

Exibir apenas o que estiver disponível no sistema.

\---

\# 8. ATALHOS RÁPIDOS

Criar seção de atalhos, se as respectivas funcionalidades já existirem.

Exemplos:

* Novo Cliente
* Novo Agendamento
* Lançar Receita
* Nova Despesa
* Enviar Mensagem
* Ver Relatórios

Mostrar apenas ações realmente existentes e autorizadas para o usuário.

Respeitar permissões.

\---

\# 9. TELA DE CLIENTES

Adaptar a tela atual de clientes/pessoas para um padrão moderno.

Usar:

* Page Header;
* botão de nova inclusão;
* busca;
* filtros;
* tabela responsiva;
* status badge;
* paginação;
* ações claras.

No desktop:

Tabela normal.

No mobile:

Evitar tabela quebrada horizontalmente.

Priorizar:

* cards;
* linhas adaptadas;
* colunas essenciais.

\---

\# 10. AGENDA

Adaptar a Agenda para visual moderno.

Se o sistema atual já tiver agenda:

* manter a funcionalidade;
* melhorar apenas a apresentação.

Pode utilizar:

* dia;
* semana;
* mês;

apenas se esses modos já existirem.

Não implementar funcionalidade complexa nova só por causa da referência.

Usar:

* cards de agendamento;
* cores suaves;
* horários claros;
* profissional/unidade/sala quando existirem.

\---

\# 11. FINANCEIRO

Modernizar as telas financeiras existentes.

Usar:

* cards de receita;
* despesa;
* saldo;
* gráficos existentes;
* categorias;
* filtros;
* período;
* tabelas responsivas.

Manter toda regra financeira existente.

Não alterar cálculo financeiro.

\---

\# 12. RESPONSIVIDADE

Este requisito é obrigatório.

Testar visualmente em:

* 1440 px;
* 1280 px;
* 1024 px;
* 768 px;
* 430 px;
* 390 px.

No mobile:

* sidebar vira drawer;
* menu fecha ao navegar;
* cards reorganizam em uma coluna;
* grids reduzem corretamente;
* botões importantes continuam acessíveis;
* tabelas não quebram a tela;
* modais não ultrapassam viewport;
* formulários ficam legíveis;
* nenhum componente deve gerar scroll horizontal desnecessário.

\---

\# 13. ACESSIBILIDADE

Preservar:

* contraste;
* foco visível;
* navegação por teclado;
* aria-label onde necessário;
* labels em campos;
* botões reais no lugar de div clicável;
* tamanho de área clicável adequado.

\---

\# 14. DARK MODE

Se a arquitetura atual permitir sem grande impacto, preparar suporte a dark mode.

Não priorizar dark mode sobre a entrega principal.

Primeiro garantir excelente light mode.

\---

\# 15. NÃO USAR MOCKS DE NEGÓCIO

Não criar dados falsos para fazer a tela parecer bonita.

É permitido usar placeholder apenas durante desenvolvimento interno, desde que removido antes da conclusão.

A versão final deve usar:

* dados reais;
* estados vazios;
* skeletons;
* mensagens adequadas quando não houver dados.

\---

\# 16. TRATAMENTO DE ESTADOS

Toda tela relevante deve prever:

* loading;
* sucesso;
* vazio;
* erro;
* sem permissão;
* sem dados;
* timeout quando aplicável.

Evitar telas quebradas ou completamente vazias.

\---

\# 17. PERMISSÕES

Respeitar o sistema existente.

Se o usuário não tiver permissão:

* não exibir ações indevidas;
* não expor botões indevidos;
* manter proteção das rotas existentes.

Não mover autorização apenas para a camada visual.

\---

\# 18. MÓDULOS E PLANOS

A sidebar e ações devem respeitar a disponibilidade de módulos já definida pelo sistema.

Não colocar regra de Plan/Subscription diretamente nos componentes visuais.

Usar a camada existente responsável por isso.

\---

\# 19. ORGANIZAÇÃO DO CÓDIGO

Evitar:

* CSS duplicado;
* componentes gigantes;
* estilos inline desnecessários;
* magic numbers;
* hardcode de tenant;
* hardcode de usuário;
* hardcode de plano;
* hardcode de módulos.

Manter o padrão arquitetural já existente.

\---

\# 20. EXECUÇÃO

Trabalhar de forma incremental.

Ordem sugerida:

1. mapear frontend atual;
1. identificar componentes reaproveitáveis;
1. definir tokens visuais;
1. implementar shell;
1. implementar sidebar;
1. implementar topbar;
1. adaptar Dashboard;
1. adaptar Clientes;
1. adaptar Agenda;
1. adaptar Financeiro;
1. revisar responsividade;
1. revisar acessibilidade;
1. revisar consistência visual;
1. executar build;
1. executar testes existentes;
1. corrigir problemas encontrados.

Avançar automaticamente quando uma etapa estiver concluída, validada e sem bloqueios.

Não pedir autorização etapa por etapa.

Parar somente se encontrar:

* decisão arquitetural relevante;
* impacto em autenticação/autorização;
* impacto em multi-tenancy;
* impacto em Subscription/Plan;
* impacto em billing;
* operação destrutiva;
* necessidade de alterar contratos críticos;
* conflito sério com arquitetura existente.

\---

\# 21. VALIDAÇÃO OBRIGATÓRIA

Ao final executar, conforme disponível no projeto:

* npm install, somente se necessário;
* build do frontend;
* lint;
* testes;
* testes de responsividade que já existirem.

Não alterar testes apenas para fazê-los passar sem justificativa.

\---

\# 22. NÃO COMMITAR AUTOMATICAMENTE

Não criar commit.

Ao final deixar tudo no working tree e apresentar relatório.

\---

\# 23. RELATÓRIO FINAL

Entregar:

STATUS: OK | BLOQUEADO | PARCIAL

\## Resumo

\## Arquivos criados

\## Arquivos alterados

\## Componentes criados

\## Design System criado/adaptado

\## Telas adaptadas

\## Responsividade validada

\## Permissões preservadas

\## Multi-tenancy preservado

\## Subscription/Plan preservados

\## Build

\## Testes

\## Pendências

\## Sugestões para próxima fase

Também informar explicitamente:

* nenhuma mudança arquitetural relevante foi realizada; ou
* quais mudanças precisam de decisão antes de continuar.

\---

\# RESULTADO VISUAL ESPERADO

O Nexora deve transmitir:

* produto SaaS premium;
* clareza;
* confiança;
* organização;
* modernidade;
* boa densidade de informação;
* excelente experiência desktop;
* excelente experiência mobile;
* identidade própria;
* consistência entre módulos.

Use as imagens fornecidas como \*\*referência visual\*\*, não como especificação rígida.

Não copie literalmente textos, números ou funcionalidades inexistentes das imagens.

Preserve a arquitetura existente e adapte o frontend real do projeto.
