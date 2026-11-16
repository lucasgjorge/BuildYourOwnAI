# language: pt
@bdd @chat @roteamento
Funcionalidade: Conversa com assistentes da organização
  Como usuário de uma organização
  Quero enviar perguntas pelo chat e direcioná-las automaticamente
  Para obter respostas do assistente mais adequado

  Contexto:
    Dado que estou autenticado
    E possuo uma organização com assistentes configurados

  Cenário: Enviar pergunta com escolha automática
    Quando envio uma pergunta com a opção de escolha automática
    Então o sistema deve selecionar um assistente elegível da organização
    E deve retornar o resultado da pergunta

  Cenário: Enviar pergunta diretamente para um assistente
    Dado que existe um assistente disponível na organização
    Quando seleciono esse assistente e envio uma pergunta
    Então a pergunta deve ser encaminhada diretamente ao assistente selecionado

  Cenário: Não misturar assistentes de organizações diferentes
    Dado que possuo duas organizações com assistentes distintos
    Quando envio uma pergunta no chat da primeira organização com escolha automática
    Então somente os assistentes elegíveis da primeira organização devem ser considerados

  Cenário: Registrar uma lacuna quando nenhuma resposta for encontrada
    Quando envio uma pergunta que nenhum assistente consegue responder
    Então o sistema deve identificar que não houve correspondência
    E deve registrar uma lacuna associada à organização

  Cenário: Informar quando não há assistente disponível para roteamento
    Dado que a organização não possui assistentes com a descrição de uso preenchida
    Quando envio uma pergunta com escolha automática
    Então o sistema deve informar que não há assistente disponível

  Cenário: Manter a conversa enquanto a tela estiver aberta
    Dado que já enviei mensagens no chat da organização
    Quando continuo utilizando a mesma tela
    Então as mensagens da conversa devem permanecer visíveis na thread atual
