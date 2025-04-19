# app.py
from flask import Flask, jsonify, request
from flask_cors import CORS
from pymongo import MongoClient

app = Flask(__name__)
CORS(app)

# Conexión a MongoDB
client = MongoClient('mongodb://localhost:27017/')
db = client['GormazAR']
images_col = db['Gormaz_coleccion']
users_col  = db['users']
stats_col  = db['stats']

# Inicializa estadísticas globales (ahora usa average_session_time y sessions_count)
if stats_col.count_documents({"_id": "global"}) == 0:
    stats_col.insert_one({
        "_id": "global",
        "unique_users": 0,
        "users_completed": 0,
        "sessions_count": 0,
        "average_session_time": 0.0
    })

# Inicializa documentos de imágenes si no existen
initial_docs = [
    {"id": "irlSoldier", "name": "Soldier in north wall", "scans": 0},
    {"id": "irlDate",    "name": "Gothic inscryption in north wall", "scans": 0},
    {"id": "irlMonk",    "name": "Pointing monk in hastial", "scans": 0}
]
for doc in initial_docs:
    if images_col.count_documents({"id": doc["id"]}, limit=1) == 0:
        images_col.insert_one(doc)

@app.route('/')
def home():
    return jsonify({"message": "Bienvenido a la API de Gormaz"}), 200

@app.route('/registerUser/<user_id>', methods=['POST'])
def register_user(user_id):
    if not users_col.find_one({"user_id": user_id}):
        users_col.insert_one({"user_id": user_id, "scanned": [], "completed": False})
        stats_col.update_one({"_id": "global"}, {"$inc": {"unique_users": 1}})
        return jsonify({"message": f"Usuario {user_id} registrado exitosamente."}), 201
    return jsonify({"message": "Usuario ya registrado."}), 200

@app.route('/increment/<doc_id>', methods=['POST'])
def increment_counter(doc_id):
    user_id = request.form.get("user_id")
    if not user_id:
        return jsonify({"error": "Falta el user_id en la solicitud."}), 400

    result = images_col.update_one({"id": doc_id}, {"$inc": {"scans": 1}})
    if result.modified_count == 0:
        return jsonify({"error": f"No se pudo incrementar el contador para {doc_id}"}), 400

    doc = images_col.find_one({"id": doc_id})
    users_col.update_one({"user_id": user_id}, {"$addToSet": {"scanned": doc_id}})

    user    = users_col.find_one({"user_id": user_id})
    scanned = user.get("scanned", [])
    if len(scanned) == 3 and not user.get("completed", False):
        users_col.update_one({"user_id": user_id}, {"$set": {"completed": True}})
        stats_col.update_one({"_id": "global"}, {"$inc": {"users_completed": 1}})

    return jsonify({
        "name":         doc["name"],
        "scans":        doc["scans"],
        "user_scanned": scanned
    }), 200

@app.route('/endSession/<user_id>', methods=['POST'])
def end_session(user_id):
    # Obtener duración enviada
    duration = request.form.get("duration")
    if duration is None:
        return jsonify({"error": "Falta el campo 'duration'."}), 400
    try:
        duration = float(duration)
    except ValueError:
        return jsonify({"error": "El valor de 'duration' no es válido."}), 400

    # Leer estadística actual
    stats = stats_col.find_one({"_id": "global"})
    current_count = stats.get("sessions_count", 0)
    current_avg   = stats.get("average_session_time", 0.0)

    # Calcular nueva media
    new_count = current_count + 1
    new_avg   = (current_avg * current_count + duration) / new_count

    # Actualizar sólo sessions_count y average_session_time
    stats_col.update_one(
        {"_id": "global"},
        {"$set": {
            "sessions_count": new_count,
            "average_session_time": new_avg
        }}
    )

    return jsonify({
        "session_duration":        duration,
        "average_session_time":    new_avg
    }), 200

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
