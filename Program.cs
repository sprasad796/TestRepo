using Pulumi;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using System.Collections.Generic;
using Pulumi.Crds.ExternalSecrets;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;
using Kubernetes = Pulumi.Kubernetes;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Crds.ExternalSecrets.V1;
using Pulumi.Kubernetes.Types.Inputs.ExternalSecrets.V1;
using Pulumi.Utilities;

return await Deployment.RunAsync( () => {
    const string ns = "crd-test";
    
    var k8sProvider = new Kubernetes.Provider( "test-provider", new Kubernetes.ProviderArgs {
        Namespace = ns
    }, new CustomResourceOptions { } );

    _ = new Namespace( "test-namespace", new NamespaceArgs {
        Metadata = new ObjectMetaArgs {
            Name = ns,
        }
    }, new CustomResourceOptions { Provider = k8sProvider } );

    var esOperator = new Release( "external-secrets-operator", new ReleaseArgs {
        Name = "external-secrets",
        Chart = "external-secrets",
        Version = "1.2.0",
        RepositoryOpts = new RepositoryOptsArgs {
            Repo = "https://charts.external-secrets.io"
        }
    }, new CustomResourceOptions { Provider = k8sProvider } );

    var secretStore = new SecretStore( "secret-store", new SecretStoreArgs {
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
        }
    }, new CustomResourceOptions { Provider = k8sProvider, IgnoreChanges = [ "status" ] } );
    
    // uncomment the below lines to get error: Preview failed: resource 'crd-test/secret-store-10e7357d' does not exist
    // when running `pulumi up`
    
    // var brokenRead = secretStore.Metadata.Apply( s => 
    //     SecretStore.Get( "secret-store-read", $"{s.Namespace}/{s.Name}", new CustomResourceOptions { Provider = k8sProvider } )
    //         .Spec
    //         .Apply( s => s.Provider.Fake.Data )
    // );
    
    // this works as expected:
    var taintedMetadata = Output.Tuple( secretStore.Metadata, secretStore.Id ).Apply( args => args.Item1 );
    var workingRead = taintedMetadata.Apply( s => 
        SecretStore.Get( "secret-store-read", $"{s.Namespace}/{s.Name}", new CustomResourceOptions { Provider = k8sProvider } )
            .Spec
            .Apply( s => s.Provider.Fake.Data )
    );
    
    // it also happens with built-in k8s objects!
    
    var testSvc = new Service( "svc", new ServiceArgs {
        Spec = new ServiceSpecArgs {
            Selector = {
                { "foo", "bar" }
            }
        }
    }, new CustomResourceOptions { Provider = k8sProvider } );
    
    // uncomment the below lines to get error: Preview failed: resource 'crd-test/svc-0be05419' does not exist
    // when running `pulumi up`
    
    // var brokenRead = testSvc.Metadata.Apply( s => 
    //     Service.Get( "svc-read", $"{s.Namespace}/{s.Name}", new CustomResourceOptions { Provider = k8sProvider } )
    //         .Spec
    //         .Apply( s => s.Selector )
    // );
    
    // this works as expected:
    var svcTaintedMetadata = Output.Tuple( secretStore.Metadata, testSvc.Id ).Apply( args => args.Item1 );
    var svcWorkingRead = svcTaintedMetadata.Apply( s => 
        Service.Get( "svc-read", $"{s.Namespace}/{s.Name}", new CustomResourceOptions { Provider = k8sProvider } )
            .Spec
            .Apply( s => s.Selector )
    );
} );
