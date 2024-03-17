# Guia de Uso da Automação para Extração de Dados em PDF 

 

## Introdução 

Este guia fornecerá instruções sobre como usar a automação desenvolvida para extrair dados de faturas em PDF usando a API OCR.Space. Esta solução automatiza o processo de leitura e análise de faturas em formato PDF, tornando-o mais eficiente e preciso. 

 

## Pré-requisitos 

### Você vai precisar possuir: 

- Uma conta na API OCR.Space com uma chave de API válida, você pode conseguir uma chave gratuita para a API clicando nesse link: https://ocr.space/ocrapi/freekey 

- Ambiente de desenvolvimento configurado com .NET Framework. 

- Acesso aos arquivos PDF que deseja processar. 

 

## Configuração 

- Clone ou baixe o código-fonte da automação para sua máquina local usando esse link: https://github.com/victorrattesdev/OcrSpace-Desafio-Victor_Rattes. 

 

- Abra o projeto em seu ambiente de desenvolvimento. 

- Lembre-se de instalar o pacote NuGet RestSharp para trabalhar com requisições HTTP e o Newtonsoft.JSON para lidar com o OUTPUT em JSON. 

- Substitua o valor da variável apiKey pela sua própria chave de API OCR.Space. 

 

## Execução da Automação 

- Organize todos os arquivos PDF que deseja processar em um diretório específico e altere a variável ‘directoryPath’ para o diretório que contenha os arquivos que deseja processar. 

- Agora basta executar o programa e aguardar a conclusão, os dados serão salvos em um arquivo JSON único no mesmo diretório para cada PDF. 

 
