# Checklist de produção

- [ ] `what-if` e Bicep aprovados; região/SKUs/custos confirmados
- [ ] OIDC e GitHub Environment protegidos; nenhum client secret Azure
- [ ] Key Vault completo e Managed Identity/RBAC testados
- [ ] PostgreSQL privado, TLS, PITR 14 dias e restore ensaiado
- [ ] ACR sem admin, imagem SHA e migration job controlado
- [ ] domínios/TLS gerenciados e CORS/AllowedHosts explícitos
- [ ] Resend/remetente e webhook Mercado Pago produtivos verificados
- [ ] alertas com destinatário e ação operacional testados
- [ ] Release build, testes, dependency audit, secret scan e smoke aprovados
- [ ] commit SHA, imagem, revision e migrations registrados
