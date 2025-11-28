# database/db_manager.py
import psycopg2
import numpy as np
from config import DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD

class DBManager:
    def __init__(self):
        try:
            self.conn = psycopg2.connect(
                host=DB_HOST,
                port=DB_PORT,
                dbname=DB_NAME,
                user=DB_USER,
                password=DB_PASSWORD
            )
            self.cursor = self.conn.cursor()
            self.create_table()
        except psycopg2.Error as e:
            raise RuntimeError(f"Database connection failed: {e}")

    def create_table(self):
        try:
            self.cursor.execute("""
            CREATE TABLE IF NOT EXISTS faces(
                id SERIAL PRIMARY KEY,
                name VARCHAR(255) NOT NULL UNIQUE,
                embedding FLOAT8[] NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            """)
            self.cursor.execute("""
            CREATE INDEX IF NOT EXISTS idx_faces_name ON faces(name);
            """)
            self.conn.commit()
        except psycopg2.Error as e:
            self.conn.rollback()
            raise RuntimeError(f"Failed to create table: {e}")

    def insert_face(self, name, embedding):
        try:
            embedding_list = embedding.tolist()
            self.cursor.execute(
                "INSERT INTO faces (name, embedding) VALUES (%s, %s)",
                (name, embedding_list)
            )
            self.conn.commit()
        except psycopg2.IntegrityError:
            self.conn.rollback()
            raise ValueError(f"Face with name '{name}' already exists")
        except psycopg2.Error as e:
            self.conn.rollback()
            raise RuntimeError(f"Failed to insert face: {e}")

    def update_face(self, name, new_embedding):
        try:
            embedding_list = new_embedding.tolist()
            self.cursor.execute(
                "UPDATE faces SET embedding = %s WHERE name = %s",
                (embedding_list, name)
            )
            self.conn.commit()
            if self.cursor.rowcount == 0:
                raise ValueError(f"No face found with name '{name}'")
        except psycopg2.Error as e:
            self.conn.rollback()
            raise RuntimeError(f"Failed to update face: {e}")

    def delete_face(self, name):
        try:
            self.cursor.execute("DELETE FROM faces WHERE name = %s", (name,))
            self.conn.commit()
            if self.cursor.rowcount == 0:
                raise ValueError(f"No face found with name '{name}'")
        except psycopg2.Error as e:
            self.conn.rollback()
            raise RuntimeError(f"Failed to delete face: {e}")

    def fetch_all_embeddings(self):
        try:
            self.cursor.execute("SELECT name, embedding FROM faces")
            rows = self.cursor.fetchall()
            database = {}
            for name, emb in rows:
                database[name] = np.array(emb, dtype=np.float32)
            return database
        except psycopg2.Error as e:
            raise RuntimeError(f"Failed to fetch embeddings: {e}")

    def get_face_by_name(self, name):
        try:
            self.cursor.execute(
                "SELECT name, embedding FROM faces WHERE name = %s",
                (name,)
            )
            result = self.cursor.fetchone()
            if result:
                return {result[0]: np.array(result[1], dtype=np.float32)}
            return None
        except psycopg2.Error as e:
            raise RuntimeError(f"Failed to fetch face: {e}")

    def face_exists(self, name):
        try:
            self.cursor.execute(
                "SELECT EXISTS(SELECT 1 FROM faces WHERE name = %s)",
                (name,)
            )
            return self.cursor.fetchone()[0]
        except psycopg2.Error as e:
            raise RuntimeError(f"Failed to check face existence: {e}")

    def close(self):
        if self.cursor:
            self.cursor.close()
        if self.conn:
            self.conn.close()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()


if __name__ == "__main__":
     db = DBManager()
     db.delete_face("reda")