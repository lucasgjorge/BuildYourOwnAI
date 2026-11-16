# language: pt
@bdd @seguranca @regras
Funcionalidade: Regras transversais de acesso e limites
  Como usuário do BuildYourOwnAI
  Quero que meus dados e recursos sejam protegidos
  Para utilizar o sistema com segurança

  Cenário: Exigir autenticação nas rotas protegidas
    Dado que não estou autenticado
    Quando tento acessar uma funcionalidade protegida da API
    Então o acesso deve ser recusado

  Cenário: Não revelar recursos pertencentes a outros usuários
    Dado que existe um recurso pertencente a outro usuário
    Quando tento acessar esse recurso
    Então o sistema deve responder como recurso não encontrado

  Cenário: Aplicar limite de chamadas às operações de IA
    Dado que estou autenticado
    Quando ultrapasso o limite de 20 chamadas por minuto nas operações sujeitas a esse controle
    Então novas chamadas dentro do período limitado devem ser restringidas
