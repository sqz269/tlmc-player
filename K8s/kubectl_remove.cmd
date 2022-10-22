kubectl.exe delete -f .\musicdata-pgsql-pv-pvc.yaml
kubectl.exe delete -f .\musicdata-pgsql-depl.yaml
kubectl.exe delete -f .\musicdata-pgsql-svc.yaml

kubectl.exe delete -f .\musicdata-api-depl.yaml
kubectl.exe delete -f .\musicdata-api-svc.yaml