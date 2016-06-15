## Azure Batch Explorer

![Azure Batch Explorer][1]<br/>

The **Azure Batch Explorer** is a Windows Presentation Foundation (WPF) application used for viewing, managing, monitoring, and debugging entities within an Azure Batch account. While this application is not officially supported, it is updated periodically, and is an invaluable tool not only for those new to Batch, but anyone developing or managing Batch applications.

Some features of the Batch Explorer:

- List Batch entities like pools, compute nodes, jobs, tasks, and schedules, and view their properties
- View task status and execution information
- List and download files from compute nodes
- Create user accounts on compute nodes and download RDP files for remote connection
- Resize pools and reboot, reimage, or delete compute nodes
- Create and delete pools, jobs, tasks, and job schedules
- Monitor task execution with the *Heat Map*

### Using the Batch Explorer

To manage entities in your Batch account with the Batch Explorer, you must first configure it by adding your account information. **Build** and **run** the application using *Visual Studio 2015 or above*, then perform the following to add your account information and connect to the Batch service:

1. Click **Accounts** > **Add** > **Default Account Manager**
2. In the *Add Account* dialog that is displayed, add an **Account Alias**, a unique identifier for this account within Batch Explorer
3. Enter the **Batch Service URL**, **Account** name, and account **Key** in the other three textboxes. These values can be found within the Batch Account blade within the [Azure Portal][portal].
4. Click **OK**

After clicking **OK** in the last step, the Batch Explorer automatically connects to your account. You may now start exploring the entities within your Batch account. You can start by refreshing an entity type via the **Refresh** menu,  selecting one in the top-left pane, then viewing its details in the top- and middle-right panes.

[portal]: http://portal.azure.com
[1]: BatchExplorer.jpg "Batch solution workflow (full diagram)"
