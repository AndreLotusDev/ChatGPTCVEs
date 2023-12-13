using System;
using System.Collections.Generic;
using System.IO;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using OpenAI.API;

var filePath = Directory.GetCurrentDirectory() + "/allitems.csv";
var data = ReadCsvFile(filePath);
// 'data' now contains a list of CVEEntry objects

static List<CVEEntry> ReadCsvFile(string filePath)
{
    List<CVEEntry> entries = new List<CVEEntry>();
    using (var reader = new StreamReader(filePath))
    {
        var actualLine = 0;
        bool isFirstRow = true;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (isFirstRow)
            {
                isFirstRow = false;
                continue; // Skip the header row
            }
            
            //skip until line 8 position
            if (actualLine < 9)
            {
                actualLine++;
                continue;
            }

            if (line.ToUpper().Contains("DO NOT USE THIS CANDIDATE NUMBER") 
                || line.ToUpper().Contains("CANDIDATES MUST BE REVIEWED") || line.ToUpper().Contains("RESERVED"))
            {
                continue; // Skip the rows with these strings
            }
            {
                var columns = line.Split(','); // Adjust the delimiter if necessary
                var entry = new CVEEntry
                {
                    Name = columns[0],
                    Status = columns[1],
                    Description = columns[2],
                    // Map other properties here
                };
                entries.Add(entry);
            }
            
            actualLine++;
        }
    }

    return entries;
}

// MongoDB setup
const string connectionString = "mongodb://localhost:27017"; 
var client = new MongoClient(connectionString);
var database = client.GetDatabase("temp"); 
var collection = database.GetCollection<CVEEntry>("CVEEntries"); 

//clean colletion
collection.DeleteMany(_ => true);

// Insert data into MongoDB
data = data.Where(w => w.Description != string.Empty && w.Description != "Description").Take(3).ToList();
InsertDataIntoMongoDB(data);

// Retrieve data from MongoDB (example)
var retrievedData = RetrieveDataFromMongoDB().Take(10);

var authentication = new APIAuthentication("");
var api = new OpenAIAPI(authentication); 

foreach (var cveToGetARecomendationAction in retrievedData)
{
    var prompt = $"what i should do to avoid this CVE: {cveToGetARecomendationAction.Name}\nDescription: {cveToGetARecomendationAction.Description}";
    var response = api.Completions.CreateCompletionAsync(prompt, temperature: 0.8, max_tokens: 100);
    response.Wait();
    
    var responseText = response.Result.Completions[0].Text;
    Console.WriteLine(responseText);
}

void InsertDataIntoMongoDB(List<CVEEntry> entries)
{
    collection.InsertMany(entries);
}

List<CVEEntry> RetrieveDataFromMongoDB()
{
    return collection.Find(_ => true).ToList();
}

public class CVEEntry
{
    [BsonId]
    public ObjectId Id { get; set; } 
    public string Name { get; set; }
    public string Status { get; set; }
    public string Description { get; set; }
    // Add other properties as per your CSV columns
}