using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt.Chunks;

public class ChatRulesPromptChunk : IPromptChunk
{
    public bool ShouldBeAdded(AgentRunData data) => true;

    public string GetContent(AgentRunData data)
    {
        var isChannel = data.Channel != null;
        var isDm = data.DmChannel != null;

        if (isChannel)
        {
            return ChannelRules();
        }

        if (isDm)
        {
            return DmRules();
        }

        return string.Empty;
    }

    private string ChannelRules()
    {
        return """
## Chat (channel) rules
VERY IMPORTANT!

0. USE GetMessages and PostMessage tools FREQUENTLY!
These tools are your THE ONLY WAY of communicating with the user and other agents in the chat, and for getting information about the chat context. Use them EXTENSIVELY and FREQUENTLY. Always use them when you need to communicate something to the user or other agents, or when you need more information about the chat context.
If you just respond in the chat without using these tools, your message may not be seen by the user or other agents, and you may miss important information about the chat context. So always use these tools to ensure that your messages are seen and that you have the most up-to-date information about the chat context.
IMPORTANT! When calling GetMessages, try to provide the 'after' parameter with the timestamp of the last message you've seen. This is crucial for efficiency - it returns only new messages since that time, reducing context usage. Use the 'postedAt' field from the last message you received as the 'after' value. Call GetMessages without 'after' on your very first turn when you have no chat history yet.

1. ROLE-PLAYING.
Always respond in a way that is consistent with your agent description and prompt.

2. WORK BALANCING & TURN SKIPPING.
Remember that you are not the only agent in the chat. Be mindful of other agents' personalities, descriptions, and prompts when crafting your responses.
Do NOT try to respond to each message in the chat.
If you see that another agent is much better suited to answer a question or perform a task, it's often best to let that agent respond instead of you.
If this case, use the WaitForNextMessage tool to skip your turn and let the other agent respond.

""";
    }

    private string DmRules()
    {
        return """
## Chat (DM) rules
VERY IMPORTANT!

0. USE GetMessages and PostMessage tools FREQUENTLY!
These tools are your THE ONLY WAY of communicating with the user and other agents in the chat, and for getting information about the chat context. Use them EXTENSIVELY and FREQUENTLY. Always use them when you need to communicate something to the user or other agents, or when you need more information about the chat context.
If you just respond in the chat without using these tools, your message may not be seen by the user or other agents, and you may miss important information about the chat context. So always use these tools to ensure that your messages are seen and that you have the most up-to-date information about the chat context.
IMPORTANT! When calling GetMessages, try to provide the 'after' parameter with the timestamp of the last message you've seen. This is crucial for efficiency - it returns only new messages since that time, reducing context usage. Use the 'postedAt' field from the last message you received as the 'after' value. Call GetMessages without 'after' on your very first turn when you have no chat history yet.

1. ROLE-PLAYING.
Always respond in a way that is consistent with your agent description and prompt.

2. YOU ARE THE ONLY AGENT IN THIS CHAT.
This is a DM (direct message) chat, so there are no other agents here. You are the only agent in this chat, and you are talking directly to the user. So you should respond to the user directly, without worrying about other agents in the chat. You don't need to skip turns for other agents, since there are no other agents in this chat.
You should answer ALL user messages in this chat, since you are the only agent here. Do not skip any user messages, since there are no other agents to handle them. Always respond to user messages in a timely manner, since you are the only agent here and the user is expecting a response from you.

""";
    }
}
