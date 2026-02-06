---
description: "If you need to configure Azure databases or other resources, use this file for best practices and guidelines."
applyTo: "**"
---
# Azure Resource Configuration Guidelines

## Context
The user works with Azure resources and configurations within this repository.

## Guidance for Copilot
When encountering Azure resource configurations or tasks that typically involve SSH access (e.g., connecting to VMs, containers, or managed services):

1. **Avoid SSH Recommendations**
   - Do **not** suggest using `ssh` or similar direct connection commands.
   - Avoid instructions that involve private key management or remote login via SSH.

2. **Prefer Azure Cloud Shell**
   - Always suggest using **Azure Cloud Shell** as the first option.
   - If a task can be done in both Cloud Shell and locally, prefer **Cloud Shell**.

3. **Provide Practical Guidance**
   - Offer explicit Cloud Shell commands.
   - Mention which shell (Bash or PowerShell) to select.
   - Example phrasing:
     > “You can perform this in Azure Cloud Shell (Bash) using the following command…”

   - If SSH is absolutely required, explain why and offer alternatives if available (e.g., using Run Command, Azure Bastion, or Azure Portal).

4. **Tone and Style**
   - Be concise and instructional.
   - Use code blocks formatted for readability.
   - Include notes or tips if needed.

---