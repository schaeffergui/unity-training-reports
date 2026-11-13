# Unity Training Reports

Sistema de **aulas, avaliação e relatório em PDF** para simuladores de máquinas agrícolas em Unity (C#).
Extraído de um simulador profissional de treinamento; nomes de empresa e marcas foram removidos.

> Este repositório é uma **vitrine de código**: depende de pacotes comerciais e de classes do simulador
> (Rewired, Vehicle Physics Pro, iTextSharp, Docnet), então não compila isolado.

## O que o sistema faz

| Parte | Arquivos | Descrição |
|---|---|---|
| Aulas e tarefas | `Runtime/Training/TrainingManager.cs` | Gerencia aulas de trator, plantadeira, pulverizador e colheitadeira: tarefas em sequência, pontuação e rastreio de desempenho |
| Painel de tarefas | `Runtime/Training/TrainingTaskUI.cs` | Interface das tarefas durante a aula |
| Relatório em PDF | `Runtime/Reports/LessonPdfExporter.cs` | Gera o PDF da aula: notas, gráficos de velocidade e RPM, mapa do trajeto, observações e recomendações para o aluno, perda de grãos e eficiência |
| Tela de fim de aula | `Runtime/Reports/LessonReportUI.cs`, `LessonReportData.cs` | Resumo ao concluir a aula |
| Visualizador de PDF | `Runtime/UI/LessonPdfViewer.cs`, `Runtime/Reports/LessonPdfDocument.cs` | Abre o PDF dentro do jogo, renderização assíncrona com cancelamento, validação de caminho e cópia segura |
| Testes | `Tests/`, `Editor/` | Testes NUnit da pontuação por área de implemento e verificações do visualizador de PDF |

## Destaques técnicos
- Pontuação que penaliza operação repetida ou fora da zona, proporcional à área do implemento.
- Relatório com observações geradas a partir dos erros do aluno e estimativa de desperdício por retrabalho.
- Visualizador de PDF que rejeita URLs, caminhos de rede e arquivos inválidos ou grandes demais.

## Observações
- Textos da interface estão em português (pt-BR).
- Logos e imagens do relatório não estão incluídos.
- Sem licença de reuso: todos os direitos reservados ao autor, salvo indicação futura.
