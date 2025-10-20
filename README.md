**ValAI – Assistente de IA para o Valheim**

ValAI é um plugin que desenvolvi para o Valheim que integra a API da OpenAI diretamente ao chat do jogo.
O jogador pode conversar com uma inteligência artificial (apelidada de THOR) sobre estratégias, dicas e informações relacionadas ao universo de Valheim — tudo sem sair do game.

 Estrutura do Projeto

ValAI/
│
├── ValAI/                      # Código-fonte principal (C#)
│   └── ValAI.cs
│
├── APISelector/                # Automação Python para configuração da API
│   ├── APISelector.py
│   └── APISelector.exe
│
├── README.md                   # Este arquivo
└── LICENSE                     # Licença (opcional)


Funcionalidades Principais
  Plugin C# (ValAI)

Carrega automaticamente a chave de API da OpenAI do arquivo aedenthorn.ValAI.cfg.

Intercepta comandos de chat /ai <mensagem> dentro do jogo.

Envia a mensagem do jogador à OpenAI via requisição HTTP.

Exibe a resposta de forma formatada no chat como se fosse o personagem THOR.

Coleta informações contextuais, como o inventário do jogador, para respostas mais úteis.

Envia mensagens de boas-vindas automáticas ao carregar o mundo.

🔹 Automação Python (APISelector)

Cria uma interface simples que permite ao usuário inserir ou selecionar sua chave de API sem editar arquivos manualmente.

Identifica automaticamente o arquivo de configuração correto (aedenthorn.ValAI.cfg) na pasta:

(C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\config)

Atualiza o campo ApiKey com a chave informada.

Pode ser distribuído como executável (.exe), facilitando o uso por usuários não técnicos.

Requisitos

Valheim (versão Steam)

BepInEx instalado (necessário para plugins C#) (https://thunderstore.io/package/bbepis/BepInExPack/versions/) Entre na primeira pasta e jogue todos os arquivos na pasta principal do game.

OpenAI API Key válida (obtenha em https://platform.openai.com)

Instalar o Plugin ValAI

Copie o arquivo compilado ValAI.dll para:

C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins

Execute o jogo uma vez para que o BepInEx gere automaticamente a configuração:

C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\config\aedenthorn.ValAI.cfg

Se desejar, edite esse arquivo manualmente e adicione sua chave no campo:

ApiKey = sua_chave_aqui

Alternativamente, use o APISelector.exe descrito abaixo para configurar automaticamente.

Configurar a API com o Executável Python

Abra o APISelector.exe localizado na pasta APISelector/.

A aplicação exibirá uma janela solicitando sua chave da OpenAI.

Digite a chave e selecione “Salvar”.

O programa localizará automaticamente o arquivo aedenthorn.ValAI.cfg e atualizará a chave.

Após salvar, basta abrir o jogo — o ValAI já estará pronto para uso.

Uso In-Game

Dentro do jogo, abra o chat e use o comando:

/ai Como faço para derrotar o Moder?

O assistente THOR responderá diretamente no chat com estratégias e dicas sobre o tema.

Como Funciona (Visão Técnica)
Plugin (C#)

O método Chat_InputText_Patch intercepta a entrada do jogador via Harmony Patch.

O texto é processado e, se iniciar com /ai, é repassado para CallOpenAiAndReply().

O plugin envia uma requisição POST para https://api.openai.com/v1/chat/completions com modelo gpt-3.5-turbo.

A resposta é tratada manualmente via parsing JSON e exibida no chat por reflexão.

O plugin também tenta coletar o inventário do jogador para enriquecer o contexto do prompt.

Automação (Python)

O script APISelector.py utiliza tkinter para exibir uma interface gráfica.

Ele varre o diretório BepInEx/config para localizar aedenthorn.ValAI.cfg.

Atualiza o valor de ApiKey e garante persistência.

Pode ser convertido em executável com o comando:

pyinstaller --onefile --noconsole APISelector.py

O executável pode ser distribuído junto com o plugin.

Integração entre os dois componentes

**Primeiro uso:** 
O Jogador abre o game com o ValAI.dll no:
C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins 

Após carregar a CFG na pasta config

O jogador abre o APISelector.exe, escolhe sua chave e salva.

Durante o jogo:

O ValAI lê a chave salva na config.

Ao digitar /ai, a mensagem é enviada à OpenAI usando a chave configurada.

Resposta:

O conteúdo retornado é processado e exibido no chat do Valheim como resposta de THOR.

**Tecnologias Utilizadas**

| Componente        | Tecnologia         | Descrição                         |
| ----------------- | ------------------ | --------------------------------- |
| Backend do Plugin | C# (.NET)          | Plugin para BepInEx               |
| Automação         | Python 3 + Tkinter | Interface gráfica simples         |
| Comunicação       | OpenAI API         | Modelo GPT-3.5-Turbo              |
| Mod Framework     | HarmonyLib         | Hook no método de chat do Valheim |
| Build Python      | PyInstaller        | Criação de executável standalone  |

Licença

Este projeto está licenciado sob a MIT License.
Sinta-se livre para modificar, redistribuir e contribuir.

Autor

Gabriel Moura Cidro
Desenvolvedor e entusiasta em automação, jogos e integração com IA.
Linkedin:(https://www.linkedin.com/in/gabriel-cidro-669119238/)GitHub(https://github.com/GabrielCidro)