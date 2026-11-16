# language: pt
@bdd @fontes @previa
Funcionalidade: Visualização das fontes utilizadas nas respostas
  Como usuário que consulta um assistente
  Quero conferir os trechos dos documentos utilizados na resposta
  Para verificar a origem das informações apresentadas

  Contexto:
    Dado que estou autenticado
    E possuo uma organização com documentos processados

  Cenário: Exibir fontes quando a resposta foi encontrada
    Dado que uma pergunta foi respondida com conteúdo encontrado nos documentos
    Quando a resposta é apresentada no chat
    Então as fontes que sustentam a resposta devem ser exibidas

  Cenário: Agrupar fontes por documento
    Dado que uma resposta utiliza vários trechos de um mesmo documento
    Quando as fontes são apresentadas
    Então os trechos devem aparecer agrupados por documento

  Cenário: Abrir a prévia de um trecho citado
    Dado que uma resposta apresenta uma fonte
    Quando clico no trecho citado
    Então a prévia do documento deve ser aberta ao lado do chat
    E o trecho selecionado deve ficar destacado

  Cenário: Exibir trechos vizinhos para contextualização
    Dado que a prévia de um trecho foi aberta
    Quando o conteúdo é carregado
    Então o sistema deve apresentar o trecho selecionado e seus trechos vizinhos

  Cenário: Não exibir fontes como evidência quando a resposta não foi encontrada
    Dado que o sistema não encontrou conteúdo para responder à pergunta
    Quando o resultado é apresentado
    Então fontes que não sustentam uma resposta não devem ser apresentadas como evidência
