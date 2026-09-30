using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using OAI.Models;

namespace OAI.Data
{
    public static class DbInitializer
    {
        public static void Initialize(OAIDbContext context)
        {
            context.Database.EnsureCreated();
            EnsureSettingsTables(context);
        }

        private static void EnsureSettingsTables(OAIDbContext context)
        {
            context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[dbo].[AppSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppSettings] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [GeminiApiKey] NVARCHAR(300) NOT NULL DEFAULT '',
        [GeminiModelName] NVARCHAR(80) NOT NULL DEFAULT 'gemini-2.0-flash',
        [IsSetupComplete] BIT NOT NULL DEFAULT 0,
        [MonitoringIntervalMinutes] INT NOT NULL DEFAULT 60
    );
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.AppSettings', 'IsSetupComplete') IS NULL
        ALTER TABLE [dbo].[AppSettings] ADD [IsSetupComplete] BIT NOT NULL DEFAULT 0;
    IF COL_LENGTH('dbo.AppSettings', 'MonitoringIntervalMinutes') IS NULL
        ALTER TABLE [dbo].[AppSettings] ADD [MonitoringIntervalMinutes] INT NOT NULL DEFAULT 60;
END

IF OBJECT_ID(N'[dbo].[EmailSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[EmailSettings] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [IsEnabled] BIT NOT NULL DEFAULT 0,
        [EmailAddress] NVARCHAR(200) NOT NULL DEFAULT '',
        [ImapServer] NVARCHAR(200) NOT NULL DEFAULT 'imap.gmail.com',
        [ImapPort] INT NOT NULL DEFAULT 993,
        [UseSsl] BIT NOT NULL DEFAULT 1,
        [EncryptedAppPassword] NVARCHAR(2000) NOT NULL DEFAULT '',
        [PollingIntervalMinutes] INT NOT NULL DEFAULT 15,
        [MinMatchScoreThreshold] FLOAT NOT NULL DEFAULT 70.0
    );
END

IF COL_LENGTH('dbo.UserProfiles', 'Education') IS NULL
    ALTER TABLE [dbo].[UserProfiles] ADD [Education] NVARCHAR(MAX) NOT NULL DEFAULT '';
IF COL_LENGTH('dbo.UserProfiles', 'Keywords') IS NULL
    ALTER TABLE [dbo].[UserProfiles] ADD [Keywords] NVARCHAR(MAX) NOT NULL DEFAULT '';

IF COL_LENGTH('dbo.JobApplications', 'Salary') IS NULL
    ALTER TABLE [dbo].[JobApplications] ADD [Salary] NVARCHAR(150) NOT NULL DEFAULT '';
IF COL_LENGTH('dbo.JobApplications', 'IsOpen') IS NULL
    ALTER TABLE [dbo].[JobApplications] ADD [IsOpen] BIT NOT NULL DEFAULT 1;
IF COL_LENGTH('dbo.JobApplications', 'HrEmail') IS NULL
    ALTER TABLE [dbo].[JobApplications] ADD [HrEmail] NVARCHAR(250) NULL;
IF COL_LENGTH('dbo.JobApplications', 'ApplyUrl') IS NULL
    ALTER TABLE [dbo].[JobApplications] ADD [ApplyUrl] NVARCHAR(1000) NOT NULL DEFAULT '';
IF COL_LENGTH('dbo.JobApplications', 'Advice') IS NULL
    ALTER TABLE [dbo].[JobApplications] ADD [Advice] NVARCHAR(MAX) NOT NULL DEFAULT '';
");
        }
    }
}
