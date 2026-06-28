namespace ConfigAdmin.Application.RemoteSync;

public readonly record struct JobProgressUpdate(string Message, bool WriteToJournal = true);
