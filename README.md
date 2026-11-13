# Training Reports (pacote Unity)

Sistema de **aulas, pontuação e relatório em PDF** para simuladores de máquinas agrícolas em Unity (C#).
Extraído de um simulador profissional de treinamento; nomes de empresa e marcas foram removidos.

## Instalação

1. No Unity: **Window > Package Manager > + > Add package from git URL...**
2. Cole:

```
https://github.com/schaeffergui/unity-training-reports.git
```

Para a versão em desenvolvimento: `https://github.com/schaeffergui/unity-training-reports.git#dev`

Também funciona por `Packages/manifest.json`:

```json
"com.schaeffergui.training-reports": "https://github.com/schaeffergui/unity-training-reports.git"
```

## Requisitos
- Unity 6 (6000.0 ou superior) e `com.unity.ugui` (instalado automaticamente).
- Instale no seu projeto: [Rewired](https://assetstore.unity.com/packages/tools/utilities/rewired-21676), Vehicle Physics Pro, iTextSharp e Docnet (pacotes de terceiros, não incluídos).
- O código usa classes do simulador (`RDSceneManager`, `RDStudent`, controladores de máquina). Como o pacote não tem `asmdef`, ele compila junto com o seu projeto e encontra essas classes. Desacoplar isso por interfaces está no roadmap.

## O que o pacote faz

| Parte | Arquivos | Descrição |
|---|---|---|
| Aulas e tarefas | `Runtime/Training/TrainingManager.cs` | Aulas de trator, plantadeira, pulverizador e colheitadeira: tarefas em sequência, pontuação e rastreio de desempenho |
| Painel de tarefas | `Runtime/Training/TrainingTaskUI.cs` | Interface das tarefas durante a aula |
| Relatório em PDF | `Runtime/Reports/LessonPdfExporter.cs` | PDF com notas, gráficos de velocidade e RPM, mapa do trajeto, observações e recomendações, perda de grãos e eficiência |
| Fim de aula | `Runtime/Reports/LessonReportUI.cs`, `LessonReportData.cs` | Resumo ao concluir a aula |
| Visualizador de PDF | `Runtime/UI/LessonPdfViewer.cs`, `Runtime/Reports/LessonPdfDocument.cs` | Abre o PDF no jogo, renderização assíncrona com cancelamento, validação de caminho e cópia segura |
| Testes | `Tests/`, `Editor/` | Testes NUnit da pontuação por área de implemento e verificações do visualizador |

## Destaques técnicos
- Pontuação que penaliza operação repetida ou fora da zona, proporcional à área do implemento.
- Observações geradas a partir dos erros do aluno e estimativa de desperdício por retrabalho.
- Visualizador que rejeita URLs, caminhos de rede e arquivos inválidos ou grandes demais.

## Notas
- Textos da interface em português (pt-BR). Logos e imagens do relatório não estão incluídos.
- Sem licença de reuso: todos os direitos reservados ao autor, salvo indicação futura.
