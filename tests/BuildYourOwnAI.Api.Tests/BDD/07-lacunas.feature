# language: pt
@bdd @lacunas
Funcionalidade: Gerenciamento de lacunas de conhecimento
  Como responsável pela organização
  Quero responder ou dispensar perguntas sem resposta
  Para complementar a base de conhecimento da organização

  Contexto:
    Dado que estou autenticado
    E existe uma lacuna aberta na minha organização

  Cenário: Listar lacunas abertas
    Quando acesso a lista de lacunas
    Então devo visualizar as lacunas abertas disponíveis para minha conta

  Cenário: Responder uma lacuna
    Quando envio uma resposta para a lacuna
    Então a lacuna deve ser respondida
    E a resposta deve se tornar um documento da organização

  Cenário: Dispensar uma lacuna
    Quando solicito dispensar a lacuna
    Então ela deve deixar de aparecer como lacuna aberta

  Cenário: Impedir acesso à lacuna de outro usuário
    Dado que existe uma lacuna pertencente a outro usuário
    Quando tento responder ou dispensar essa lacuna
    Então o sistema deve responder como recurso não encontrado
