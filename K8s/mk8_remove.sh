microk8s kubectl delete -f ./pgsql-musicdata-svc.yaml
microk8s kubectl delete -f ./pgsql-musicdata-depl.yaml
microk8s kubectl delete -f ./pgsql-musicdata-pv-pvc.yaml

microk8s kubectl delete -f ./musicdata-api-svc.yaml
microk8s kubectl delete -f ./musicdata-api-depl.yaml