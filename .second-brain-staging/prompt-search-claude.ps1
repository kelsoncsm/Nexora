[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

try {
    $rawInput = [Console]::In.ReadToEnd()
    try { $eventData = $rawInput | ConvertFrom-Json } catch { $eventData = $null }

    $prompt = ''
    if ($eventData) {
        foreach ($field in @('prompt', 'user_prompt', 'message', 'text', 'input')) {
            if ($eventData.PSObject.Properties.Name -contains $field -and $eventData.$field) { $prompt = [string]$eventData.$field; break }
        }
    }
    if ([string]::IsNullOrWhiteSpace($prompt)) { Write-Output '{}'; exit 0 }

    $vault = 'C:\SEGUNDO CÉREBRO'
    $categories = @('01 Projetos', '02 Padrões', '03 Stack e Ferramentas', '04 Preferências', '05 Decisões-ADR')
    $stopwords = @('ainda','algo','aquela','aquele','aqui','arquivo','assim','cada','coisa','com','como','da','das','de','depois','do','dos','essa','esse','esta','este','fazer','isso','mais','mesmo','muito','na','nas','não','nos','para','pela','pelo','pode','poderia','por','preciso','projeto','qual','quando','que','quero','ser','sobre','sua','tem','uma','usar','usando','você')
    $terms = @($prompt.ToLowerInvariant() -split '[^a-z0-9áéíóúâêôãõç+#.-]+' | Where-Object { $_.Length -gt 2 -and $stopwords -notcontains $_ } | Select-Object -Unique)
    if ($terms.Count -eq 0) { Write-Output '{}'; exit 0 }

    $matches = foreach ($category in $categories) {
        $directory = Join-Path $vault $category
        if (-not (Test-Path -LiteralPath $directory)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $directory -Filter '*.md' -File -ErrorAction SilentlyContinue) {
            if ($file.Name -eq '_Índice.md') { continue }
            $content = [string](Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 -ErrorAction SilentlyContinue)
            $name = $file.BaseName.ToLowerInvariant(); $body = $content.ToLowerInvariant(); $score = 0
            foreach ($term in $terms) { if ($name.Contains($term)) { $score += 5 } elseif ($body.Contains($term)) { $score += 1 } }
            if ($score -gt 0) { [PSCustomObject]@{ Score = $score; Category = $category; Name = $file.BaseName; Content = $content } }
        }
    }
    $selected = @($matches | Sort-Object Score, Name -Descending | Select-Object -First 5)
    if ($selected.Count -eq 0) { Write-Output '{}'; exit 0 }

    $blocks = foreach ($match in $selected) {
        $excerpt = $match.Content.Trim()
        if ($excerpt.Length -gt 2400) { $excerpt = $excerpt.Substring(0, 2400) + "`n[conteúdo truncado]" }
        "#### [[$($match.Category)/$($match.Name)]] (relevância $($match.Score))`n$excerpt"
    }
    $context = "### Segundo Cérebro — contexto relacionado automático`nUse o conteúdo abaixo antes de responder ou implementar. Ele reúne aprendizados de qualquer projeto do computador; em caso de conflito, respeite as instruções do projeto atual.`n`n" + ($blocks -join "`n`n")
    @{ hookSpecificOutput = @{ hookEventName = 'UserPromptSubmit'; additionalContext = $context } } | ConvertTo-Json -Compress -Depth 6
} catch {
    Write-Output '{}'
}
