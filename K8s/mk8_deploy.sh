microk8s kubectl apply -f ./musicdata-pgsql-pv-pvc.yaml
microk8s kubectl apply -f ./musicdata-pgsql-depl.yaml
microk8s kubectl apply -f ./musicdata-pgsql-svc.yaml

microk8s kubectl apply -f ./musicdata-api-depl.yaml
microk8s kubectl apply -f ./musicdata-api-svc.yaml