﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using static Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource;

namespace PandaClaus.LettersApi;

public class GoogleSheetsClient
{
    private readonly string _sheetName;
    private readonly string _spreadsheetId;
    private readonly SheetsService _sheetsService;

    public GoogleSheetsClient(IConfiguration configuration)
    {
        _spreadsheetId = configuration["SpreadsheetId"]!;
        _sheetName = configuration["SheetName"]!;

        var credential = GetCredentialsFromFile();
        _sheetsService = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Panda Claus"
        });
    }

    public async Task<List<Letter>> FetchLetters()
    {
        var range = $"{_sheetName}!A:R";
        var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, range);
        var response = await request.ExecuteAsync();

        return MapToLetters(response.Values);
    }

    public async Task<Letter> FetchLetter(int rowNumber)
    {
        var range = $"{_sheetName}!A{rowNumber}:R{rowNumber}";
        var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, range);
        var response = await request.ExecuteAsync();

        var letter = MapLetter(rowNumber, response.Values.First());
        return letter;
    }

    private List<Letter> MapToLetters(IList<IList<object>> values)
    {
        var letters = new List<Letter>();
        var rowNumber = 2;

        foreach (var row in values.Skip(1))
        {
            if (row.Count < 11)
                continue;

            var letter = MapLetter(rowNumber, row);

            rowNumber++;
            letters.Add(letter);
        }

        return letters;
    }

    private static Letter MapLetter(int rowNumber, IList<object> row)
    {
        var letter = new Letter
        {
            RowNumber = rowNumber,
            Added = DateTime.Parse(row[0].ToString() ?? string.Empty),
            Email = row[1].ToString() ?? string.Empty,
            ParentName = row[2].ToString() ?? string.Empty,
            PhoneNumber = row[4].ToString() ?? string.Empty,
            Address = row[5].ToString() ?? string.Empty,
            PaczkomatCode = row[6].ToString() ?? string.Empty,
            ChildenNamesAndAges = row[7].ToString() ?? string.Empty,
            Reason = row[8].ToString() ?? string.Empty,
            ImageUrls = string.IsNullOrWhiteSpace(row[10].ToString())
                ? new List<string>()
                : row[10].ToString()!.Split(',').Select(url => url.Trim()).ToList(),

            // optional cells
            IsHidden = !string.IsNullOrWhiteSpace(GetCellOrEmptyString(row, 12)),
            IsAssigned = !string.IsNullOrWhiteSpace(GetCellOrEmptyString(row, 13)),
            AssignedTo = GetCellOrEmptyString(row, 14),
            AssignedToEmail = GetCellOrEmptyString(row, 15),
            AssignedToPhone = GetCellOrEmptyString(row, 16),
            AssignedToDescription = GetCellOrEmptyString(row, 17)
        };

        return letter;
    }

    private static string GetCellOrEmptyString(IList<object> row, int index)
    {
        if (row.Count >= index + 1)
        {
            return row[index].ToString()!;
        }

        return string.Empty;
    }

    private GoogleCredential GetCredentialsFromFile()
    {
        using var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read);
        var credential = GoogleCredential.FromStream(stream).CreateScoped(SheetsService.Scope.Spreadsheets);
        return credential;
    }

    public async Task AssignLetter(LetterAssignment assignment)
    {
        var range = $"{_sheetName}!N{assignment.RowNumber}:R{assignment.RowNumber}";

        var valuesToUpdate = new List<object>{ "tak", assignment.Name, assignment.Email, assignment.PhoneNumber, assignment.Description };
        var valueRange = new ValueRange
        {
            Values = new List<IList<object>> { valuesToUpdate }
        };
        var updateRequest = _sheetsService.Spreadsheets.Values.Update(valueRange, _spreadsheetId, range);
        updateRequest.ValueInputOption = UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await updateRequest.ExecuteAsync();
    }
}