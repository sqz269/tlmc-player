kubectl.exe apply -f .\auth-pgsql-pv-pvc.yaml
kubectl.exe apply -f .\auth-pgsql-depl.yaml
kubectl.exe apply -f .\auth-pgsql-svc.yaml

kubectl.exe apply -f .\auth-api-depl.yaml
kubectl.exe apply -f .\auth-api-svc.yaml

kubectl.exe apply -f .\musicdata-pgsql-pv-pvc.yaml
kubectl.exe apply -f .\musicdata-pgsql-depl.yaml
kubectl.exe apply -f .\musicdata-pgsql-svc.yaml

kubectl.exe apply -f .\musicdata-api-depl.yaml
kubectl.exe apply -f .\musicdata-api-svc.yaml