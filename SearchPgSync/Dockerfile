FROM python:3.7
ARG WORKDIR=/code
RUN mkdir $WORKDIR
COPY ./schema.json $WORKDIR
COPY ./bootstrap.sql $WORKDIR
WORKDIR $WORKDIR
RUN pip install git+https://github.com/toluaina/pgsync.git
COPY ./wait-for-it.sh wait-for-it.sh
COPY ./runserver.sh runserver.sh
RUN chmod +x wait-for-it.sh
RUN chmod +x runserver.sh
