# Rotação de secrets

Inventário: connection string, JWT signing key, Mercado Pago access token/webhook secret e Resend API key.

1. Gere nova credencial no sistema de origem com sobreposição quando suportada.
2. Adicione nova versão do secret no Key Vault sem expor o valor em logs, terminal compartilhado ou ticket.
3. Reinicie/crie revisão do Container App e confirme que a referência sem versão foi atualizada.
4. Execute smoke seguro e monitore erros.
5. Revogue a credencial antiga somente após confirmação.

Rotação da chave JWT invalida sessões existentes e deve ser comunicada. Rotação de banco pode exigir janela coordenada. Use RBAC de menor privilégio e audite acessos ao Key Vault.
