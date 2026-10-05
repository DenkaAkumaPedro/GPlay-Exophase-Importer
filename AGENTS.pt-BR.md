# AGENTS.pt-BR.md

Tradução de [AGENTS.md](AGENTS.md). Em caso de divergência, o arquivo em inglês é a
fonte da verdade.

Add-on do Playnite (C# / .NET Framework 4.6.2) que importa jogos do Google Play–Android
e o tempo de jogado do Exophase para o Playnite. Versão atual: 0.1.0.

## Comandos

```powershell
.\build.ps1                                             # build Release -> bin\Release
.\build.ps1 -Install                                    # build + copia para -ExtensionsDir
.\build.ps1 -Pack                                       # build + Toolbox.exe pack -> dist\*.pext
dotnet test GPlayExophaseImporter.Tests\GPlayExophaseImporter.Tests.csproj
```

- `dotnet test` sem argumentos também funciona a partir da raiz do repo (resolve o
  `GPlayExophaseImporter.slnx`; **não** existe arquivo `.sln`).
- Não existe nenhuma configuração de lint / formatação / typecheck. O build é a única
  verificação disponível.
- `-Install` aponta por padrão para `D:\Progamas\Biblioteca\Playnite\Extensions` — o
  Playnite está instalado fora do local padrão nesta máquina (repare no typo
  "Progamas"). Use `-ExtensionsDir` caso isso mude.
- `-Pack` precisa do `Toolbox.exe` do Playnite; o script procura em
  `%LOCALAPPDATA%\Playnite`, depois no diretório de instalação custom, depois no `PATH`.
- **Reinicie o Playnite depois do `-Install`.** O manifesto da extensão é lido apenas
  na inicialização, e o processo em execução mantém o metadata antigo em memória.
  Feche o Playnite *antes*: uma DLL de extensão carregada fica travada, então a cópia
  falha silenciosamente enquanto o app estiver aberto.

## Restrições obrigatórias

- **O plugin é `net462` com `LangVersion 7.3`.** Não use sintaxe C# 8+ (`using var`,
  switch expressions, `new` com tipo inferido, nullable reference types,
  ranges/indices) e não aumente o target framework — a DLL é carregada pelo processo
  desktop do Playnite. Só o SDK do .NET 10 está instalado; `net462` compila através do
  pacote `Microsoft.NETFramework.ReferenceAssemblies`.
- **`Playnite.SDK.dll` nunca deve chegar à saída do add-on.** Isso é garantido por
  `CopyLocalLockFileAssemblies=false` mais o target `RemoveSdkFromOutput` em
  `GPlayExophaseImporter.csproj`. Uma cópia dentro da pasta da extensão causa conflito
  de carregamento de assemblies no Playnite. Não "corrija" a ausência dela em `bin/`.
- **A UI de configurações é WPF só em código** (`Settings/GPlaySettingsView.cs`) — sem
  XAML, sem designer. Para adicionar um setting: uma property em `GPlaySettings`
  (`ObservableObject` + `SetValue`), depois faz o bind pelo caminho em string
  `"Settings.PropertyName"`.
- **Os DTOs do Exophase exigem `[SerializationPropertyName("...")]`**
  (`Exophase/ExophaseModels.cs`). O `Serialization.FromJson` do Playnite não usa a
  nomenclatura padrão, então properties em PascalCase apenas falham em silêncio.
- **O csproj do plugin fica na raiz do repo e usa glob `**/*.cs`**, então qualquer nova
  pasta na raiz é compilada automaticamente. `GPlayExophaseImporter.Tests\**` está
  excluído explicitamente — mantenha essa exclusão, senão o plugin tentará compilar os
  fontes de teste do xunit.

## Contratos cuja alteração perde dados do usuário

- GUID do `Id` — `GPlayExophaseImporterPlugin.cs:19`. Alterá-lo faz o Playnite perder o
  rastreamento de todos os jogos já importados por este add-on.
- Formato do `GameId` `exophase:{master_id}` — `Exophase/ExophaseClient.cs:26`. Gravado
  em `Game.GameId` e comparado em `OnLibraryUpdated`; mudar o formato cria duplicatas em
  vez de atualizar as entradas existentes.
- `GPlayLabel = "Google Play"` — `GPlayExophaseImporterPlugin.cs:25`. Gravado em
  **ambos** os campos `Source` e `Platforms`. O provider Exophase do PlayniteAchievements
  casa "google play"/"android" para cair no provider GooglePlay dele — renomear isso
  quebra silenciosamente o reconhecimento de conquistas de todos os usuários.
- `OnLibraryUpdated` / `ForcePlaytimeSync` existem porque o Playnite só importa tempo de
  jogo para jogos *novamente adicionados*, a menos que a opção global dele esteja em
  "Always" — o que também mudaria Steam e as demais bibliotecas. Mantenha a
  sincronização restrita a `PluginId == Id`.

## Testes

- **O host de teste está sujeito à política de Controle de Aplicativo (WDAC) desta
  máquina, e essa política é baseada em hash.** Qualquer mudança no binário do plugin —
  mesmo só no csproj, como metadados de assembly — gera um hash novo, e aí a
  `GPlayExophaseImporter.dll` recém-compilada é recusada no load com
  `FileLoadException ... 0x800711C7` e **os 40 testes falham**. Reverter a alteração
  restaura o hash antigo e os testes voltam a ficar verdes. Não "corrija" isso no
  código; não é reproduzível fora desta máquina.
- Com o binário sem alterações, `dotnet test` reporta **38 aprovados / 2 falhos**. As
  duas falhas são `DeserializationTests.*`: a mesma política bloqueia o
  `Playnite.SDK.dll` (net462) quando o host de teste tenta refletir sobre
  `SerializationPropertyNameAttribute`.
- O próprio Playnite **não** é afetado — o processo dele carrega o `Playnite.SDK.dll`
  net462 e a DLL do plugin instalada sem problema. Só o host de teste .NET 10 tropeça
  na política.
- Causa raiz dessa limitação, e da gambiarra nos testes: o Playnite injeta o próprio
  serializador na inicialização, então `Serialization.FromJson` não roda em um host de
  teste comum. `DeserializationTests` reimplementa o mapeamento com Newtonsoft mais um
  `DefaultContractResolver` local que respeita `SerializationPropertyNameAttribute`.
- Só membros `internal static` de `ExophaseClient` e dos DTOs são alcançáveis a
  partir dos testes (`InternalsVisibleTo("GPlayExophaseImporter.Tests")` em
  `Properties/AssemblyInfo.cs`). Qualquer coisa que toque `HttpClient`, `api.WebViews` ou
  o banco de dados do Playnite não é testável por unidade — verifique manualmente.
- Loop manual: `.\build.ps1 -Install` → reiniciar o Playnite → menu do add-on → Update
  Library. Falhas no mundo real quase sempre são perfil não público ou usuário errado,
  não código. O Exophase fica atrás do Cloudflare; `ExophaseClient` recorre à view
  Chromium offscreen do Playnite, que não pode ser chamada na thread da UI.

## Metadata que o Playnite exibe

O Playnite tira nome/autor/versão do add-on do **`extension.yaml` que está instalado**,
e não da árvore de fontes, do `.pext` nem dos atributos do assembly. Então um
`Author:` correto no repo não prova nada sobre o que o app exibe — compare a cópia
instalada (`Extensions\GPlayExophaseImporter\extension.yaml`) antes de depurar.

- Existem duas superfícies com duas fontes diferentes:
  - **Detalhes do add-on instalado** (tela de Add-ons, menu Extensions) → `extension.yaml`
    local. Resolvido localmente com `-Install` + reinício.
  - **Aba Add-on Library** → renderizada a partir de
    `https://playnite.link/addons.html`, alimentada pelo repo
    `JosefNemec/PlayniteAddonDatabase`. O `manifests/addon.yaml` só aparece lá depois
    que um PR for mergeado; nada local muda isso.
- A versão do add-on é **apenas** o `Version` do `extension.yaml`. `<AssemblyVersion>` /
  `<Version>` no csproj são cosméticos e **não** mudam o que o Playnite mostra — subir
  eles para "corrigir" uma divergência de versão não funciona.

## Release

- A versão vive em três lugares que precisam concordar: `extension.yaml`
  (`Version`, `Id`, `Module`), `manifests/installer.yaml` (`Packages[].Version`,
  `RequiredApiVersion: 6.0.0`, `ReleaseDate`, `PackageUrl`) e a tag do git.
- `manifests/` serve ao banco oficial de add-ons do Playnite
  (`JosefNemec/PlayniteAddonDatabase`). **Não** faz parte do add-on empacotado e nunca é
  copiado para `bin/`.
- O Toolbox nomeia o pacote como `GPlayExophaseImporter_<versão com pontos como
  sublinhados>.pext` (`0.1.0` → `GPlayExophaseImporter_0_1_0.pext`). O `PackageUrl` em
  `manifests/installer.yaml` precisa bater com o nome do asset enviado.
- O envio do `.pext` para um GitHub release é um passo manual; nada neste repo faz isso.
- Atualize a seção "Status" do README no mesmo commit do release.

## Convenções

Não há `.editorconfig`, analyzer nem config de formatter no repo. Siga o código ao redor:
indentação de 4 espaços, chaves no estilo Allman, membros em `PascalCase`, campos
privados em `_camelCase`, comentários XML em membros não óbvios. O projeto é
intencionalmente pequeno e secundário/experimental (veja o README) — prefira mudanças
mínimas e focadas.