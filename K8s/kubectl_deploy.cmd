kubectl.exe apply -f .\musicdata-pgsql-pv-pvc.yaml
kubectl.exe apply -f .\musicdata-pgsql-depl.yaml
kubectl.exe apply -f .\musicdata-pgsql-svc.yaml

kubectl.exe apply -f .\musicdata-api-depl.yaml
kubectl.exe apply -f .\musicdata-api-svc.yaml