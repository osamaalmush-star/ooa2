-- SQL Server LocalDB Database Creation Script for OAI App
-- Connection String: Server=(localdb)\mssqllocaldb;Database=OAIDatabase;Trusted_Connection=True;

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'OAIDatabase')
BEGIN
    CREATE DATABASE [OAIDatabase];
END
GO

USE [OAIDatabase];
GO

-- 1. UserProfiles Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserProfiles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UserProfiles] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [FullName] NVARCHAR(150) NOT NULL,
        [Email] NVARCHAR(150) NOT NULL,
        [Phone] NVARCHAR(50) NOT NULL,
        [Location] NVARCHAR(150) NOT NULL,
        [LinkedInUrl] NVARCHAR(250) NULL,
        [GitHubUrl] NVARCHAR(250) NULL,
        [PortfolioUrl] NVARCHAR(250) NULL,
        [TargetJobTitles] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [Skills] NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        [ExperienceSummary] NVARCHAR(MAX) NOT NULL DEFAULT '',
        [RawResumeContent] NVARCHAR(MAX) NULL,
        [ParsedProfileJson] NVARCHAR(MAX) NULL,
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- 2. SearchSources Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SearchSources]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[SearchSources] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PlatformName] NVARCHAR(100) NOT NULL,
        [BaseUrl] NVARCHAR(500) NOT NULL,
        [IsEnabled] BIT NOT NULL DEFAULT 1,
        [FilterKeywords] NVARCHAR(250) NULL,
        [LocationFilter] NVARCHAR(150) NULL,
        [AutoApplyEnabled] BIT NOT NULL DEFAULT 0,
        [DailyApplyLimit] INT NOT NULL DEFAULT 20,
        [LastSearchedAt] DATETIME2 NULL
    );

    -- Seed Default Search Sources
    INSERT INTO [dbo].[SearchSources] ([PlatformName], [BaseUrl], [IsEnabled], [FilterKeywords], [DailyApplyLimit])
    VALUES 
        ('LinkedIn', 'https://www.linkedin.com/jobs', 1, '.NET Developer, C# Developer', 25),
        ('Indeed', 'https://www.indeed.com', 1, 'Full Stack C# .NET', 20),
        ('Glassdoor', 'https://www.glassdoor.com', 0, 'Software Engineer .NET', 15),
        ('Wuzzuf', 'https://wuzzuf.net', 1, '.NET Core, WPF, C#', 30);
END
GO

-- 3. JobApplications Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[JobApplications]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[JobApplications] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [JobTitle] NVARCHAR(250) NOT NULL,
        [CompanyName] NVARCHAR(200) NOT NULL,
        [Location] NVARCHAR(150) NULL,
        [JobUrl] NVARCHAR(1000) NOT NULL,
        [Platform] NVARCHAR(100) NOT NULL,
        [JobDescription] NVARCHAR(MAX) NULL,
        [MatchScore] FLOAT NOT NULL DEFAULT 0.0,
        [MatchAnalysisJson] NVARCHAR(MAX) NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Discovered',
        [AppliedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Notes] NVARCHAR(MAX) NULL
    );
END
GO

-- 4. EmailMonitorLogs Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EmailMonitorLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[EmailMonitorLogs] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Sender] NVARCHAR(200) NOT NULL,
        [Subject] NVARCHAR(300) NOT NULL,
        [ReceivedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ExtractedJobLink] NVARCHAR(1000) NULL,
        [MatchScore] FLOAT NULL,
        [NotificationTriggered] BIT NOT NULL DEFAULT 0,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Processed',
        [LogDetails] NVARCHAR(MAX) NULL
    );
END
GO
