using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RestSharp;

class Program
{
    static void Main(string[] args)
    {
        string directoryPath = "C:\\Users\\liuka\\ConsoleApp1\\PDF\\";
        string apiKey = "K86927181488957";

        // Verificar se o diretório existe
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine("O diretório especificado não existe.");
            return;
        }

        // Obter todos os arquivos PDF de um diretório
        string[] pdfFiles = Directory.GetFiles(directoryPath, "*.pdf");

        // Lógica sobre os arquivos PDF da pasta
        foreach (string pdfFile in pdfFiles)
        {
            // Ler o conteúdo do PDF   
            byte[] pdfBytes = File.ReadAllBytes(pdfFile);

            // Solicitando API OCR.Space
            var client = new RestClient("https://api.ocr.space/parse/image");
            var request = new RestRequest();

            // Parâmetros API
            request.AddParameter("apikey", apiKey);
            request.AddParameter("isOverlayRequired", "true");
            request.AddParameter("filetype", "PDF");

            // Adicionar o PDF como um anexo (parte do corpo da solicitação)
            request.AddFile("file", pdfBytes, Path.GetFileName(pdfFile), "application/pdf");

            // POST
            request.Method = Method.Post;

            // Enviar a solicitação e obter a resposta
            RestResponse response = client.Execute(request);

            // Solicitação bem-sucedida
            if (response.IsSuccessful)
            {
                // Processar o conteúdo da resposta para manter apenas o texto extraído
                var jsonResponse = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                var parsedResults = jsonResponse["ParsedResults"];

                var combinedText = ""; // Combina todos os textos de todas as páginas

                foreach (var result in parsedResults)
                {
                    var ocrText = result["ParsedText"].ToString();
                    combinedText += ocrText + "\n";
                }

                var data = ExtractData(combinedText);

                // Adicionar o resultado da comparação ao dicionário de dados
                string totalString = (string)data["Total"];
                string somaPrecosString = (string)data["Soma de Preços"];

                // Remoção de caracteres não numéricos, deixando só os números e o separador decimal
                totalString = new string(totalString.Where(char.IsDigit).ToArray());
                somaPrecosString = new string(somaPrecosString.Where(char.IsDigit).ToArray());

                // Converter as strings para valores decimais
                decimal total = decimal.Parse(totalString) / 100; // Divisão por 100 por serem caracteres
                decimal somaPrecos = decimal.Parse(somaPrecosString) / 100; // Divisão por 100 por serem caracteres

                bool totalIgualSomaPrecos = total == somaPrecos;
                if (totalIgualSomaPrecos == false)
                {
                    data["Total calculado é igual ou diferente ao total lido?"] = "O total calculado é diferente do total lido.";
                }
                else
                {
                    data["Total calculado é igual ou diferente ao total lido?"] = "O total calculado é igual ao total lido.";
                }
                
                // Criação de um arquivo JSON para cada PDF na pasta, usando o mesmo nome do arquivo PDF
                string jsonFilePath = Path.Combine(directoryPath, Path.GetFileNameWithoutExtension(pdfFile) + ".json");

                var orderedKeys = new List<string> { "Invoice Number", "Date", "Billed to", "Business Number in Brazil","Tabela Details", "Soma de Preços", "Total calculado é igual ou diferente ao total lido?", "Due date" };

                // Ordem de exibição das chaves - Lógica
                var orderedData = new Dictionary<string, object>();
                foreach (var key in orderedKeys)
                {
                    if (data.ContainsKey(key))
                        orderedData.Add(key, data[key]);
                }

                // Converter o dicionário de dados para JSON
                string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(orderedData, Newtonsoft.Json.Formatting.Indented);

                File.WriteAllText(jsonFilePath, jsonData);

                Console.WriteLine($"Arquivo JSON gerado com sucesso para: {pdfFile}");
            }
            else
            {
                Console.WriteLine($"Erro ao processar o arquivo {pdfFile}: {response.ErrorMessage}");
            }
        }

        Console.WriteLine("Processo concluído.");
    }

    // Método para extrair os dados de texto do PDF
    static Dictionary<string, object> ExtractData(string ocrText)
    {
        var data = new Dictionary<string, object>();

        // Varíavel para dividir o texto em linhas
        string[] lines = ocrText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Variáveis para armazenar os valores extraídos
        string invoiceNumber = null;
        string date = null;
        string billedTo = null;
        string businessNumber = null;       
        string total = null;
        string dueDate = null;
        bool foundService = false;
        bool foundDetails = false;
        var content = new HashSet<string>(); // Usando uma lista para armazenar todas as linhas do conteúdo

        //Percorrer as linhas após leitura pelo OCR Space.
        foreach (string line in lines)
        {
            //Quando encontrar serviço percorrer até achar o delimitado no IndexOf
            if (foundService)
            {
                if (line.IndexOf("Payment details", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break;
                }
                content.Add(line.Trim());
            }

            else if (foundDetails)
            {
                foundService = true;
            }
            
            //Busca por Details na linha, e quando encontrar marca como encontrada para começar o conteúdo.
            else if (line.IndexOf("Details", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foundDetails = true;
            }

            //Quando a linha começar com o solicitado, ao achar, carregar o que estiver na mesma linha.
            if (line.StartsWith("Invoice Number:", StringComparison.OrdinalIgnoreCase))
            {

                invoiceNumber = line.Substring("Invoice Number:".Length).Trim();
                int invoiceNumberInt = int.Parse(invoiceNumber);
            }

            //Quando a linha começar com o solicitado, ao achar, carregar o que estiver na mesma linha, nesse caso, determinando a área e convertendo o obtido em uma data ISO8601.
            else if (line.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
            {
                string dateString = line.Substring("Date:".Length).Trim();
                dateString = dateString.Substring(0, Math.Min(dateString.Length, 10));
                if (!string.IsNullOrWhiteSpace(dateString))
                {
                    // Conversão para ISO8601
                    DateTime parsedDate;
                    if (DateTime.TryParse(dateString, out parsedDate))
                    {
                        date = parsedDate.ToString("s");
                    }
                    else
                    {
                        Console.WriteLine($"Erro ao converter a data: {dateString}");
                    }
                }
                else
                {
                    Console.WriteLine("A string da data está vazia.");
                }
            }

            //Quando a linha começar com o solicitado, ao achar, carregar o que estiver na mesma linha.
            else if (line.StartsWith("Billed to:", StringComparison.OrdinalIgnoreCase))
            {
                billedTo = line.Substring("Billed to:".Length).Trim();
            }

            //Quando a linha começar com o solicitado, ao achar, carregar o que estiver na mesma linha.
            else if (line.StartsWith("Business Number:", StringComparison.OrdinalIgnoreCase))
            {
                businessNumber = line.Substring("Business Number:".Length).Trim();
            }

        }

        //Aqui, desejo buscar o Due date: partindo do Service presente em meu retorno.
        int serviçoIndex = content.ToList().FindIndex(x => x.StartsWith("Service", StringComparison.OrdinalIgnoreCase));
        if (serviçoIndex != -1)
        {
            content = new HashSet<string>(content.Skip(serviçoIndex));
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("Due date: ", StringComparison.OrdinalIgnoreCase))
            {
                // Capturando o conteúdo presente em toda a linha que começa com o solicitado.
                dueDate = line.Substring("Due date: ".Length).Trim();

                if (!string.IsNullOrWhiteSpace(dueDate))
                {
                    //Conversão para ISO8601
                    DateTime parsedDueDate;
                    if (DateTime.TryParse(dueDate, out parsedDueDate))
                    {
                        dueDate = parsedDueDate.ToString("s");
                    }
                    else
                    {
                        Console.WriteLine($"Erro ao converter a data: {dueDate}");
                    }
                }
            }
        }

        int serviceIndex = content.ToList().FindIndex(x => x.StartsWith("Service", StringComparison.OrdinalIgnoreCase));
        if (serviceIndex != -1)
        {
            content = new HashSet<string>(content.Skip(serviceIndex));
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("Billed to", StringComparison.OrdinalIgnoreCase))
            {
                // Se sim, tentar capturar o nome que vem abaixo
                billedTo = GetBilledToName(lines, i + 1);
                if (!string.IsNullOrWhiteSpace(billedTo))
                {
                    data["Billed to"] = billedTo;
                }
            }
        }

        
        // Identificar o total como string e extrair o valor,
        string totalAsString = content.LastOrDefault();

        int charactersToRemove = 0;
        string moeda = string.Empty;

        if (totalAsString.StartsWith("$"))
        {
            charactersToRemove = 2;
            moeda = "$ - (USD)";
        }
        else if (totalAsString.StartsWith("R$"))
        {
            charactersToRemove = 3;
            moeda = "R$ - (BRL)";
        }
        if (charactersToRemove > 0 && totalAsString.Length >= charactersToRemove)
        {
            totalAsString = totalAsString.Substring(charactersToRemove);
        }

        string numericValue = new string(totalAsString.Where(char.IsDigit).ToArray());

        if (decimal.TryParse(numericValue, out decimal totalValue))
        {
            string formattedTotal = totalValue.ToString("#,##0.00");
            data["Total"] = formattedTotal;
            data["Moeda"] = moeda;
        }
        else
        {
            Console.WriteLine("Erro ao converter o valor do total para um valor monetário.");
        }

        int priceIndex = -1;
        int currentIndex = 0;

        foreach (string line in content)
        {
            if (line.StartsWith("Price", StringComparison.OrdinalIgnoreCase))
            {
                priceIndex = currentIndex;
                break;
            }
            currentIndex++;
        }

        if (priceIndex != -1)
        {
            // Lista para armazenar os valores de preços da tabela, começamos pegando o valor como string e transformando em decimal para tratar e formatar 
            List<decimal> precoValores = new List<decimal>();

            for (int i = priceIndex + 1; i < content.Count - 1; i++)
            {
                string line = content.ElementAt(i);
                string priceValueString = new string(line.Where(char.IsDigit).ToArray());

                if (decimal.TryParse(priceValueString, out decimal priceValue))
                {
                    string centavos = priceValueString.Substring(Math.Max(0, priceValueString.Length - 2));
                    string parteInteira = priceValueString.Substring(0, Math.Max(0, priceValueString.Length - 2));

                    // Formatando o preço com separador de milhares e considerando os dois últimos dígitos como centavos
                    string formattedPrice = string.Format("{0}.{1}", Convert.ToDecimal(parteInteira), centavos);
                    precoValores.Add(decimal.Parse(formattedPrice)); // Convertendo de volta para decimal para manter o tipo da lista
                }

            }

            //Foi criada uma lista apenas para os preços formatados, usando a lógica de separar o corpo do valor, da casa dos centavos e a partir desse ponto, decidir como montar a casa dos centavos
            List<string> precoFormatado = new List<string>();
            
            foreach (decimal preco in precoValores)
            {
                string centavos = preco.ToString().Substring(Math.Max(0, preco.ToString().Length - 2));
                string parteInteira = preco.ToString().Substring(0, Math.Max(0, preco.ToString().Length - 2));

                if (centavos == "00")
                {
                    centavos = ",00";
                }
                else
                {
                    centavos = "," + centavos;
                }
                                
                // Concatenar a parte inteira e os centavos formatados e incluir preço na lista
                string formattedPrice = parteInteira + centavos;
                precoFormatado.Add(formattedPrice);
            }

            data["Preço"] = precoFormatado;

            // Converter os preços formatados de volta para valores decimais para depois somar todos
            List<decimal> precosDecimais = precoFormatado.Select(p => decimal.Parse(p)).ToList();
            decimal somaPrecosFormatados = precosDecimais.Sum();

            // Obter a parte inteira dos valores e depois obter os centavos
            string parteInteiraSum = ((int)somaPrecosFormatados).ToString();
            string centavosSum = (somaPrecosFormatados - Math.Truncate(somaPrecosFormatados)).ToString().PadRight(3, '0').Substring(2);

            // Formatação usada para adicionar uma vírgula antes das duas últimas casas e incluir o sinal respectivo de valor
            string somaFormatada = string.Format("{0} {1},{2}", moeda == "R$ - (BRL)" ? "R$" : "$", parteInteiraSum, centavosSum);
            data["Soma de Preços"] = somaFormatada;

        }
        else
        {
            Console.WriteLine("A palavra \"Price\" não foi encontrada no conteúdo.");
        }

        
        // Encontrar o índice da primeira ocorrência da palavra "Price" dentro do Content
        int dateIndex = -1;
        int currentDateIndex = 0;

        foreach (string line in content)
        {
            if (line.StartsWith("Price (USD)", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Price (BRL)", StringComparison.OrdinalIgnoreCase))
            {
                dateIndex = currentDateIndex;
                break;
            }
            currentDateIndex++;
        }


        // Cria uma lista para armazenar os valores de "Date Tp" no formato ISO8601
        int dateToIndex = content.ToList().FindIndex(x => x.StartsWith("Date To", StringComparison.OrdinalIgnoreCase));
        if (dateToIndex != -1 && dateIndex != -1)
        {
            
            List<string> dateToList = new List<string>();

            for (int i = dateToIndex + 1; i < dateIndex; i++)
            {
                string dateString = content.ElementAt(i);

                // Conversão da string para DateTime e depois para string no formato ISO8601
                DateTime parsedDate;
                if (DateTime.TryParse(dateString, out parsedDate))
                {
                    dateToList.Add(parsedDate.ToString("s"));
                }
                else
                {
                    Console.WriteLine($"Erro ao converter a data \"{dateString}\" para o formato ISO8601.");
                }
            }
            data["Date To"] = dateToList;
        }
        else
        {
            Console.WriteLine("A palavra \"Date To\" não foi encontrada no conteúdo.");
        }


        
        // Encontrar o índice da primeira ocorrência da palavra "Price" dentro do Content
        int datePastIndex = -1;
        int DateFromIndex = 0;

        foreach (string line in content)
        {
            if (line.StartsWith("Date to", StringComparison.OrdinalIgnoreCase))
            {
                datePastIndex = DateFromIndex;
                break;
            }
            DateFromIndex++;
        }

        
        // Cria uma lista para armazenar os valores de "Date From" no formato ISO8601
        int datefromIndex = content.ToList().FindIndex(x => x.StartsWith("Date from", StringComparison.OrdinalIgnoreCase));
        if (datefromIndex != -1 && datePastIndex != -1)
        {
            List<string> datefromList = new List<string>();

            for (int i = datefromIndex + 1; i < datePastIndex; i++)
            {
                string dateString = content.ElementAt(i);

                // Conversão de string para DateTime e depois para string no formato ISO8601.
                DateTime parsedDate;
                if (DateTime.TryParse(dateString, out parsedDate))
                {
                    datefromList.Add(parsedDate.ToString("s"));
                }
                else
                {
                    Console.WriteLine($"Erro ao converter a data \"{dateString}\" para o formato ISO8601.");
                }
            }

            data["Date From"] = datefromList;
        }
        else
        {
            Console.WriteLine("A palavra \"Date From\" não foi encontrada no conteúdo.");
        }

        
        // Achar índice da primeira ocorrência da palavra "Total" dentro do Content
        int serviceDetailsIndex = -1;
        int serviceCurrentIndex = 0;

        foreach (string line in content)
        {
            if (line.StartsWith("Total", StringComparison.OrdinalIgnoreCase))
            {
                serviceDetailsIndex = serviceCurrentIndex;
                break;
            }
            serviceCurrentIndex++;
        }

        
        // Obter o objeto Service a partir da procura da palavra Details percorrendo todas as linhas até encontrar Total que demarca o limite do conteúdo dos Services
        int servicecurrentIndex = content.ToList().FindIndex(x => x.StartsWith("Details", StringComparison.OrdinalIgnoreCase));
        if (servicecurrentIndex == -1)
        {
            var serviceList = new List<string>();
           
            for (int i = servicecurrentIndex + 1; i < content.Count; i++)
            {
                string line = content.ElementAt(i);

                if (line.StartsWith("Total", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                serviceList.Add(line);
            }

            data["Service"] = serviceList;
        }
        
        // Declaração de datas
        data["Invoice Number"] = invoiceNumber;
        data["Date"] = date;
        data["Due date"] = dueDate;
        data["Billed to"] = billedTo;
        data["Business Number in Brazil"] = businessNumber;
        data["Moeda"] = moeda;
        data["Total"] = content.LastOrDefault();
        data["Content"] = lines.ToList();

        // Criar o Objeto "Pai" tabelaDetails
        var tabelaDetails = new Dictionary<string, object>();

        // Passa as datas da tabela do PDF para o objeto tabelaDetails
        tabelaDetails["Service"] = data["Service"];
        tabelaDetails["Date From"] = data["Date From"];
        tabelaDetails["Date To"] = data["Date To"];
        tabelaDetails["Moeda"] = data["Moeda"];
        tabelaDetails["Price"] = data["Preço"];
        tabelaDetails["Total"] = data["Total"];

        // Adicionar o objeto tabelaDetails ao dicionário de dados
        data["Tabela Details"] = tabelaDetails;

        return data;
    }

    // Obter o conteúdo da linha abaixo
    static string GetBilledToName(string[] lines, int startIndex)
    {
        for (int i = startIndex; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }
        return null;
    }

    // Obter o conteúdo da próxima linha não vazia
    static string GetNextLineContent(string[] lines, int startIndex)
    {
        for (int i = startIndex; i < lines.Length; i++)
        {
            string line = lines[i].Trim();           
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }
        return null;
    }
}
