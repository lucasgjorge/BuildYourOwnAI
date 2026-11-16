# language: pt
@bdd @assistentes
Funcionalidade: Gerenciamento de assistentes de IA
  Como usuário autenticado
  Quero criar e administrar assistentes dentro de uma organização
  Para responder perguntas de acordo com diferentes especialidades

  Contexto:
    Dado que estou autenticado
    E possuo uma organização cadastrada

  Cenário: Criar um assistente com organização e instruções de roteamento
    Quando cadastro um assistente informando sua organização e a descrição de quando utilizá-lo
    Então o assistente deve ser criado
    E deve ficar associado à organização informada

  Cenário: Consultar um assistente
    Dado que existe um assistente cadastrado
    Quando consulto os dados desse assistente
    Então o sistema deve retornar as informações do assistente

  Cenário: Excluir um assistente
    Dado que existe um assistente cadastrado
    Quando solicito sua exclusão
    Então o assistente não deve mais estar disponível

  Cenário: Impedir criação de assistente sem organização válida
    Quando tento criar um assistente com uma organização inexistente ou sem acesso
    Então a criação não deve ser concluída
