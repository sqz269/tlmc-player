kubectl.exe delete -f .\pgsql-musicdata-svc.yaml
kubectl.exe delete -f .\pgsql-musicdata-depl.yaml
kubectl.exe delete -f .\pgsql-musicdata-pv-pvc.yaml

kubectl.exe delete -f .\musicdata-api-svc.yaml
kubectl.exe delete -f .\musicdata-api-depl.yaml