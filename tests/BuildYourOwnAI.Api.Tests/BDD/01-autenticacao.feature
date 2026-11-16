# language: pt
@bdd @autenticacao
Funcionalidade: Autenticação de usuários
  Como visitante do BuildYourOwnAI
  Quero criar uma conta e acessar minha sessão
  Para utilizar minhas organizações e assistentes

  Cenário: Criar uma conta com dados válidos
    Dado que não existe uma conta cadastrada com o e-mail informado
    Quando envio meus dados para criar uma conta
    Então minha conta deve ser criada
    E devo conseguir iniciar uma sessão com essas credenciais

  Cenário: Impedir o cadastro com um e-mail já utilizado
    Dado que já existe uma conta cadastrada com o e-mail informado
    Quando tento criar outra conta utilizando o mesmo e-mail
    Então o cadastro não deve ser concluído

  Cenário: Iniciar sessão com credenciais válidas
    Dado que possuo uma conta cadastrada
    Quando informo meu e-mail e senha corretos
    Então devo iniciar uma sessão autenticada

  Cenário: Recusar login com credenciais inválidas
    Dado que possuo uma conta cadastrada
    Quando informo credenciais incorretas
    Então o acesso não deve ser autorizado

  Cenário: Encerrar a sessão
    Dado que estou autenticado no sistema
    Quando solicito logout
    Então minha sessão deve ser encerrada

  Cenário: Consultar informações da sessão
    Dado que estou autenticado no sistema
    Quando solicito as informações da minha conta
    Então o sistema deve retornar as informações da minha sessão
