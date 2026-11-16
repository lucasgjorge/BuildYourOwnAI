# language: pt
@bdd @organizacoes
Funcionalidade: Gerenciamento de organizações
  Como usuário autenticado
  Quero criar e administrar organizações
  Para manter meus documentos e assistentes organizados

  Cenário: Criar uma organização
    Dado que estou autenticado no sistema
    Quando informo os dados necessários para criar uma organização
    Então a organização deve ser criada
    E deve ficar disponível na minha lista de organizações

  Cenário: Listar minhas organizações
    Dado que estou autenticado
    E possuo organizações cadastradas
    Quando consulto a lista de organizações
    Então devo visualizar somente as organizações da minha conta

  Cenário: Consultar uma organização existente
    Dado que possuo uma organização cadastrada
    Quando acesso os dados dessa organização
    Então o sistema deve retornar os dados da organização

  Cenário: Excluir uma organização
    Dado que possuo uma organização cadastrada com documentos e assistentes
    Quando solicito a exclusão dessa organização
    Então a organização deve ser excluída
    E seus documentos, assistentes e lacunas associados também devem ser excluídos

  Cenário: Impedir acesso à organização de outro usuário
    Dado que existe uma organização pertencente a outro usuário
    Quando tento consultar ou excluir essa organização
    Então o sistema deve responder como recurso não encontrado
