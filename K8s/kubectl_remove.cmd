kubectl.exe apply -f .\pgsql-musicdata-pv-pvc.yaml
kubectl.exe apply -f .\pgsql-musicdata-depl.yaml
kubectl.exe apply -f .\pgsql-musicdata-svc.yaml

kubectl.exe apply -f .\musicdata-api-depl.yaml
kubectl.exe apply -f .\musicdata-api-svc.yaml