using Serilog;
using SOFD_Core.Model;
using System;
using System.Management.Automation.Runspaces;

namespace Active_Directory
{
    public class PowershellService : IDisposable
    {
        private ILogger log;
        private Runspace runspace;
        public PowershellService(ILogger log)
        {
            this.log = log;
            InitialSessionState initialSession = InitialSessionState.CreateDefault();
            initialSession.ImportPSModule(new[] { "./CustomPowershell/UserChanged.psm1" });
            runspace = RunspaceFactory.CreateRunspace(initialSession);
            runspace.Open();
        }

        public void Dispose()
        {
            runspace.Close();
            runspace.Dispose();
        }

        public void UserChanged(Person person, User user, Affiliation affiliation, OrgUnit orgUnit)
        {
            try
            {
                log.Information($"Invoking UserChanged.psm1 for user: {user.userId}");
                using (var pipeline = runspace.CreatePipeline())
                {
                    Command command = new Command("User-Changed");

                    // expose minimal dto to custom script. Expand as needed
                    var userChangedDto = new
                    {
                        uuid = person?.uuid,
                        firstname = person?.firstname,
                        surname = person?.surname,
                        chosenName = person?.chosenName,
                        user = new {
                            uuid = user?.uuid,
                            master = user?.master,
                            masterId = user?.masterId,
                            userId = user?.userId
                        },
                        affiliation = new {
                            uuid = affiliation?.uuid,
                            master = affiliation?.master,
                            employeeId = affiliation?.employeeId,
                            positionName = affiliation?.positionName,
                            affiliationType = affiliation?.affiliationType,
                            orgunit = new {
                                uuid = orgUnit?.uuid,
                                parentUuid = orgUnit?.parentUuid,
                                master = orgUnit?.master,
                                masterId = orgUnit?.masterId,
                                name = orgUnit?.GetDisplayName()
                            }
                        }
                    };

                    command.Parameters.Add("Person", userChangedDto);
                    pipeline.Commands.Add(command);
                    pipeline.Invoke();
                    if (pipeline.HadErrors)
                    {
                        var errors = pipeline.Error.ReadToEnd();
                        foreach (var error in errors)
                        {
                            Log.Error(error.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {                
                log.Error(ex, "Failed to Invoke User-Changed custom powershell command");
            }
        }
    }
}
