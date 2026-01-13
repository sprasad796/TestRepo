using Pulumi;
using Pulumi.Kubernetes.ApiExtensions;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;
using Kubernetes = Pulumi.Kubernetes;
using System.Collections.Generic;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Crds.ExternalSecrets.V1;
using Pulumi.Kubernetes.Types.Inputs.ExternalSecrets.V1;
using Pulumi.Utilities;

return await Deployment.RunAsync( () => {
    //const string ns = "crd-test";
    //const string sn = "secret-store-43d73c75";
    const string ns = "external-secrets-system";
    
    var k8sProvider = new Kubernetes.Provider( "test-provider", new Kubernetes.ProviderArgs {
        Namespace = ns
    }, new CustomResourceOptions { } );


    var ns2 = new Kubernetes.Core.V1.Namespace( "external-secrets-ns", new(){
        Metadata = new ObjectMetaArgs {
            Name = ns,
        }
    });

    // Install External Secrets Operator
    var externalSecretsOperator = new Release("external-secrets-operator", new ReleaseArgs
    {
        Chart = "external-secrets",
        SkipCrds = true,
        RepositoryOpts = new RepositoryOptsArgs
        {
            Repo = "https://charts.external-secrets.io"
        },
        Namespace = ns2.Metadata.Apply(m => m.Name),
    });

    var secretStore = new SecretStore( "secret-store",   
      new SecretStoreArgs {
        Spec = new SecretStoreSpecArgs {
            Provider = new ProviderArgs {
                Fake = new FakeProviderArgs {
                    Data = [
                        new FakeProviderDataArgs {
                            Key = "foo",
                            Value = "bar",
                            Version = "v1"
                        }
                    ]
                },
            }
        },
        Metadata = new ObjectMetaArgs {
            Namespace = ns2.Metadata.Apply(m => m.Name),
        }
    }, 
    new CustomResourceOptions { 
        DependsOn={ externalSecretsOperator }
    }
  );
    
  var workingRead = SecretStore.Get("secret-store-get", 
                                    secretStore.Id,
                                    new CustomResourceOptions { 
                                       Provider = k8sProvider,
                                       DependsOn = secretStore 
                                    } 
                     );

   var secretIdOutput = secretStore.Id.Apply( x => { return string.Format($"{x}");} );
   secretStore.Id.Apply( id => { 
         Pulumi.Log.Debug("Debug mode");
         Pulumi.Log.Debug("Hidden by deafult");
         Pulumi.Log.Info($"Created SecretStore with ID : {id}");
         return id;
        }
   );

} );
