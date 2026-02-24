namespace Worker.Models;

public record PendingQuestion(string QuestionId, string Text, DateTimeOffset AskedAt);
