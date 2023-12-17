FROM elasticsearch:8.6.2

# Install Elasticsearch plugins
RUN bin/elasticsearch-plugin install --batch analysis-kuromoji
RUN bin/elasticsearch-plugin install --batch analysis-icu