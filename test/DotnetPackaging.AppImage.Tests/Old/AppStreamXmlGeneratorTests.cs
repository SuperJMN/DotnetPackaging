using DotnetPackaging.AppImage.Core;

namespace DotnetPackaging.AppImage.Tests;

public class AppStreamXmlGeneratorTests
{
    [Fact]
    public void Generate()
    {
        var generateXml = AppStreamXmlGenerator.GenerateXml(new Metadata
        {
            Icon = Maybe<IIcon>.None,
            AppName = "AvaloniaSyncer",
            Comment = "This is an application to rule every filesystem",
            Keywords = Maybe<IEnumerable<string>>.From(["AvaloniaUI", "Files"]),
            StartupWmClass = Maybe<string>.None,
            Categories = new Categories(MainCategory.Utility, AdditionalCategory.FileManager, AdditionalCategory.FileTransfer),
            Version = "1.0.0",
            HomePage = new Uri("https://github.com/SuperJMN/avaloniasyncer"),
            License = "MIT",
            ScreenshotUrls = Maybe.From<IEnumerable<Uri>>([new Uri("https://private-user-images.githubusercontent.com/3109851/294203061-da1296d3-11b0-4c20-b394-7d3425728c0e.png?jwt=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmF3LmdpdGh1YnVzZXJjb250ZW50LmNvbSIsImtleSI6ImtleTUiLCJleHAiOjE3MTMzOTE4MjksIm5iZiI6MTcxMzM5MTUyOSwicGF0aCI6Ii8zMTA5ODUxLzI5NDIwMzA2MS1kYTEyOTZkMy0xMWIwLTRjMjAtYjM5NC03ZDM0MjU3MjhjMGUucG5nP1gtQW16LUFsZ29yaXRobT1BV1M0LUhNQUMtU0hBMjU2JlgtQW16LUNyZWRlbnRpYWw9QUtJQVZDT0RZTFNBNTNQUUs0WkElMkYyMDI0MDQxNyUyRnVzLWVhc3QtMSUyRnMzJTJGYXdzNF9yZXF1ZXN0JlgtQW16LURhdGU9MjAyNDA0MTdUMjIwNTI5WiZYLUFtei1FeHBpcmVzPTMwMCZYLUFtei1TaWduYXR1cmU9MGUwNzVjMGUwMmIyNjMzYjRjNDJlODRjM2U2M2YzYjU1ZDkxNmMwYTc4YzU3NGVmYTViNDBkNDYzZGE3ZTUzNCZYLUFtei1TaWduZWRIZWFkZXJzPWhvc3QmYWN0b3JfaWQ9MCZrZXlfaWQ9MCZyZXBvX2lkPTAifQ.jwDV9Bh-bRoqHCeo1DqKCMebdVdPVIS-dBuBGjk_dE8")]),
            Summary = "Rule all your file systems!",
            AppId = "com.SuperJMN.AvaloniaSyncer"
        });
    }
}