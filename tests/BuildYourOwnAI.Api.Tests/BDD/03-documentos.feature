# language: pt
@bdd @documentos
Funcionalidade: Gerenciamento de documentos da organização
  Como usuário autenticado
  Quero adicionar e remover documentos de uma organização
  Para fornecer conhecimento aos meus assistentes

  Contexto:
    Dado que estou autenticado
    E possuo uma organização cadastrada

  Esquema do Cenário: Enviar documento em formato suportado
    Quando envio um documento no formato "<formato>" com tamanho permitido
    Então o documento deve ser recebido para processamento
    E deve ficar associado à minha organização

    Exemplos:
      | formato |
      | PDF     |
      | TXT     |
      | MD      |

  Cenário: Listar documentos da organização
    Dado que a organização possui documentos cadastrados
    Quando consulto os documentos da organização
    Então devo visualizar os documentos associados a ela

  Cenário: Excluir um documento da organização
    Dado que a organização possui um documento cadastrado
    Quando solicito a exclusão desse documento
    Então o documento não deve mais aparecer na lista da organização

  Cenário: Recusar documento acima do limite de tamanho
    Quando envio um documento com tamanho superior a 10 MB
    Então o sistema deve rejeitar o envio

  Cenário: Recusar arquivo sem texto extraível
    Quando envio um PDF sem texto extraível
    Então o sistema deve rejeitar o processamento
    E deve informar que o conteúdo não pôde ser extraído

  Cenário: Impedir acesso ao documento de outra organização
    Dado que existe um documento pertencente a uma organização de outro usuário
    Quando tento consultar ou excluir esse documento
    Então o sistema deve responder como recurso não encontrado
