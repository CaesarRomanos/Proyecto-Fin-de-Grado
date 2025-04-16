from flask import Flask, jsonify
from flask_cors import CORS  # Importa la extensión
from pymongo import MongoClient

app = Flask(__name__)
CORS(app)  # Habilita CORS para todas las rutas

# Conexión a MongoDB
client = MongoClient('mongodb://localhost:27017/')
db = client['GormazAR']  # Nombre de la base de datos
collection = db['Gormaz_coleccion']  # Nombre de la colección

# Inicialización de los documentos
initial_docs = [
    {"id": "irlSoldier", "name": "Soldier in north wall", "scans": 0},
    {"id": "irlDate", "name": "Gothic inscryption in north wall", "scans": 0},
    {"id": "irlMonk", "name": "Pointing monk in hastial", "scans": 0}
]

for doc in initial_docs:
    if collection.count_documents({"id": doc["id"]}, limit=1) == 0:
        collection.insert_one(doc)

@app.route('/')
def home():
    return jsonify({"message": "Bienvenido a la API de Gormaz"}), 200

@app.route('/increment/<doc_id>', methods=['POST'])
def increment_counter(doc_id):
    # Incrementa el contador (scans) de forma atómica para el documento especificado
    result = collection.update_one({"id": doc_id}, {"$inc": {"scans": 1}})
    if result.modified_count:
        doc = collection.find_one({"id": doc_id})
        return jsonify({"name": doc["name"], "scans": doc["scans"]})
    return jsonify({"error": f"No se pudo incrementar el contador para {doc_id}"}), 400

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
