# language: pt
@bdd @estudar
Funcionalidade: Estudar com base nos documentos da organização
  Como usuário de uma organização
  Quero responder perguntas de múltipla escolha geradas a partir dos documentos
  Para estudar e verificar meu entendimento do conteúdo

  Contexto:
    Dado que estou autenticado
    E possuo uma organização com documentos disponíveis para estudo

  Esquema do Cenário: Criar sessão com quantidade permitida de perguntas
    Quando solicito uma sessão de estudo com <quantidade> perguntas
    Então uma sessão de estudo deve ser criada
    E as perguntas devem ser de múltipla escolha
    E o gabarito não deve ser enviado junto com a criação da sessão

    Exemplos:
      | quantidade |
      | 5          |
      | 10         |
      | 20         |

  Cenário: Criar sessão utilizando documentos selecionados
    Dado que selecionei documentos específicos da organização
    Quando inicio uma sessão de estudo
    Então as perguntas devem ser geradas com base nos documentos selecionados

  Cenário: Enviar resposta para uma pergunta
    Dado que existe uma sessão de estudo com uma pergunta não respondida
    Quando seleciono uma das alternativas e envio minha resposta
    Então o sistema deve corrigir a resposta
    E deve retornar a alternativa correta e uma explicação

  Cenário: Exibir trecho de origem após responder
    Dado que respondi uma pergunta da sessão de estudo
    Quando o sistema apresenta a correção
    Então deve apresentar o trecho do documento que fundamenta a resposta

  Cenário: Recusar quantidade de perguntas não permitida
    Quando solicito uma sessão com quantidade diferente de 5, 10 ou 20 perguntas
    Então a solicitação deve ser rejeitada
