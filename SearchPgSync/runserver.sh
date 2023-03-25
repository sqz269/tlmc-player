#! /bin/sh

./wait-for-it.sh $PG_HOST:$PG_PORT -t 60

./wait-for-it.sh $ELASTICSEARCH_HOST:$ELASTICSEARCH_PORT -t 60

./wait-for-it.sh $REDIS_HOST:$REDIS_PORT -t 60

# EXAMPLE_DIR="examples/airbnb"
# EXAMPLE_DIR="."

# python $EXAMPLE_DIR/schema-lite.py --config $EXAMPLE_DIR/schema-lite.json

# python $EXAMPLE_DIR/data.py --config $EXAMPLE_DIR/schema.json

echo "Bootstrapping Database"
bootstrap --config schema.json

echo "Starting pgSync"
pgsync --config schema.json --daemon --verbose